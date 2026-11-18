using System.CodeDom.Compiler;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace FastInsert.SqlServer.SourceGenerators
{
    [Generator]
    public sealed class FastInsertIncrementalGenerator : IIncrementalGenerator
    {
        private const string BulkInsertAttribute = "FastInsert.Core.BulkInsertAttribute";
        private const string ColumnAttribute = "System.ComponentModel.DataAnnotations.Schema.ColumnAttribute";
        private const string TableAttribute = "System.ComponentModel.DataAnnotations.Schema.TableAttribute";

        public void Initialize(IncrementalGeneratorInitializationContext context)
        {
            var entityModels = context.SyntaxProvider
                .ForAttributeWithMetadataName(
                    BulkInsertAttribute,
                    predicate: IsCandidate,
                    transform: ExtractEntityModel)
                .Where(type => type != null);

            context.RegisterSourceOutput(entityModels,
                (spc, model) => { spc.AddSource($"{model.WriterName}.g.cs", GenerateDataWriter(model)); });

            var diEntries = entityModels
                .Select((m, _) => new DiEntry(m.EntityFullName, $"FastInsert.Writers.{m.WriterName}"))
                .Collect();

            context.RegisterSourceOutput(diEntries, (spc, entries) =>
            {
                if (!entries.IsDefaultOrEmpty)
                {
                    spc.AddSource("DependencyInjectionExtensions.g.cs", GenerateDependencyInjectionExtensions(entries));
                }
            });
        }

        private static bool IsCandidate(SyntaxNode node, CancellationToken cancellation)
        {
            cancellation.ThrowIfCancellationRequested();

            switch (node)
            {
                case ClassDeclarationSyntax classDeclarationSyntax:
                    return classDeclarationSyntax.AttributeLists.Count > 0;
                case StructDeclarationSyntax structDeclarationSyntax:
                    return structDeclarationSyntax.AttributeLists.Count > 0;
                default:
                    return false;
            }
        }

        private static EntityModel ExtractEntityModel(GeneratorAttributeSyntaxContext ctx, CancellationToken ct)
        {
            if (!(ctx.TargetSymbol is INamedTypeSymbol entitySymbol))
            {
                return null;
            }

            ct.ThrowIfCancellationRequested();

            var bulkInsertAttr = ctx.Attributes
                .FirstOrDefault(a => a.AttributeClass?.ToDisplayString() == BulkInsertAttribute);
            var batchSize = 1000;

            if (bulkInsertAttr?.ConstructorArguments.Length > 0 &&
                bulkInsertAttr.ConstructorArguments[0].Value is int size && size > 0)
            {
                batchSize = size;
            }

            var tableName = entitySymbol.Name;
            var tableAttr = entitySymbol.GetAttributes()
                .FirstOrDefault(a => a.AttributeClass?.ToDisplayString() == TableAttribute);

            if (tableAttr?.ConstructorArguments.Length > 0
                && tableAttr.ConstructorArguments[0].Value is string customTableName
                && !string.IsNullOrEmpty(customTableName))
            {
                tableName = customTableName;
            }

            var properties = GetProperties(entitySymbol);
            var mappingsBuilder = ImmutableArray.CreateBuilder<PropertyMappingModel>(properties.Length);

            for (var i = 0; i < properties.Length; i++)
            {
                var prop = properties[i];
                var colName = prop.Name;
                var colAttr = prop.GetAttributes()
                    .FirstOrDefault(a => a.AttributeClass?.ToDisplayString() == ColumnAttribute);

                if (colAttr?.ConstructorArguments.Length > 0
                    && colAttr.ConstructorArguments[0].Value is string customColName
                    && !string.IsNullOrEmpty(customColName))
                {
                    colName = customColName;
                }

                mappingsBuilder.Add(new PropertyMappingModel(
                    i,
                    prop.Name,
                    colName,
                    prop.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                    prop.Type.SpecialType,
                    prop.Type.IsValueType && prop.Type.NullableAnnotation != NullableAnnotation.Annotated));
            }

            return new EntityModel(
                entitySymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                GetWriterName(entitySymbol),
                tableName,
                batchSize,
                new EquatableArray<PropertyMappingModel>(mappingsBuilder.ToImmutable()));
        }

        private static string GetWriterName(ISymbol symbol)
        {
            var name = $"{symbol.Name}Writer";
            var parent = symbol;

            while (true)
            {
                if (parent.ContainingType != null && !string.IsNullOrEmpty(parent.Name))
                {
                    parent = parent.ContainingType;
                    name = $"{parent.Name}{name}";
                }
                else
                {
                    break;
                }
            }

            return name;
        }

        private static string GenerateDataWriter(EntityModel model)
        {
            var source = new StringBuilder();

            using (var writer = new IndentedTextWriter(new StringWriter(source), "\t"))
            {
                writer.WriteLine("// Auto-generated code");
                writer.WriteLine("namespace FastInsert.Writers");
                writer.WriteLine("{");
                writer.Indent++;

                writer.WriteLine($"internal sealed class {model.WriterName} : FastInsert.SqlServer.SqlServerWriter<{model.EntityFullName}>");
                writer.WriteLine("{");
                writer.Indent++;

                writer.WriteLine($"public {model.WriterName}(Microsoft.Data.SqlClient.SqlConnection connection) : base({model.BatchSize}, connection)");
                writer.WriteLine("{");
                writer.WriteLine("}");
                writer.WriteLine();

                writer.WriteLine($"protected override System.Data.IDataReader CreateDataReader(IEnumerable<{model.EntityFullName}> data)");
                writer.WriteLine("{");
                writer.Indent++;
                writer.WriteLine("return new InternalDataReader(data);");
                writer.Indent--;
                writer.WriteLine("}");
                writer.WriteLine();

                WriteConfigureMappingsMethod(writer, model.TableName, model.Mappings);

                writer.WriteLine($"private sealed class InternalDataReader(IEnumerable<{model.EntityFullName}> data) : FastInsert.Core.DataReaderBase<{model.EntityFullName}>({model.Mappings.Length}, data)");
                writer.WriteLine("{");
                writer.Indent++;
                WriteGetValueMethod(writer, model.Mappings);
                writer.Indent--;
                writer.WriteLine("}");

                writer.Indent--;
                writer.WriteLine("}");

                writer.Indent--;
                writer.WriteLine("}");
            }

            return source.ToString();
        }

        private static string GenerateDependencyInjectionExtensions(ImmutableArray<DiEntry> entries)
        {
            var source = new StringBuilder();

            using (var writer = new IndentedTextWriter(new StringWriter(source), "\t"))
            {
                writer.WriteLine("// Auto-generated code");
                writer.WriteLine("using Microsoft.Extensions.DependencyInjection;");
                writer.WriteLine("using Microsoft.Extensions.DependencyInjection.Extensions;");
                writer.WriteLine();
                writer.WriteLine("namespace FastInsert.DependencyInjection");
                writer.WriteLine("{");
                writer.Indent++;

                writer.WriteLine("public static class ServiceCollectionExtensions");
                writer.WriteLine("{");
                writer.Indent++;

                writer.WriteLine("public static IServiceCollection AddFastInsert(this IServiceCollection services)");
                writer.WriteLine("{");
                writer.Indent++;

                foreach (var entry in entries)
                {
                    writer.WriteLine($"services.AddScoped<FastInsert.Core.IDataWriter<{entry.EntityFullName}>, {entry.WriterFullName}>();");
                }

                writer.WriteLine();
                writer.WriteLine("return services;");

                writer.Indent--;
                writer.WriteLine("}");

                writer.Indent--;
                writer.WriteLine("}");

                writer.Indent--;
                writer.WriteLine("}");
            }

            return source.ToString();
        }

        private static void WriteConfigureMappingsMethod(IndentedTextWriter writer, string tableName,
            EquatableArray<PropertyMappingModel> mappings)
        {
            writer.WriteLine("protected override void ConfigureMappings(Microsoft.Data.SqlClient.SqlBulkCopy sqlBulkCopy)");
            writer.WriteLine("{");
            writer.Indent++;

            writer.WriteLine($"sqlBulkCopy.DestinationTableName = \"{tableName}\";");

            foreach (var mapping in mappings)
            {
                writer.WriteLine($"sqlBulkCopy.ColumnMappings.Add({mapping.Index}, \"{mapping.ColumnName}\");");
            }

            writer.Indent--;
            writer.WriteLine("}");
            writer.WriteLine();
        }

        private static void WriteGetValueMethod(IndentedTextWriter writer, EquatableArray<PropertyMappingModel> mappings)
        {
            writer.WriteLine("public override object GetValue(int i)");
            writer.WriteLine("{");
            writer.Indent++;

            writer.WriteLine("switch(i)");
            writer.WriteLine("{");
            writer.Indent++;

            foreach (var mapping in mappings)
            {
                writer.WriteLine($"case {mapping.Index}: return Current.{mapping.PropertyName};");
            }

            writer.WriteLine("default: throw new IndexOutOfRangeException();");
            writer.Indent--;
            writer.WriteLine("}");

            writer.Indent--;
            writer.WriteLine("}");
        }

        private static ImmutableArray<IPropertySymbol> GetProperties(ITypeSymbol symbol)
        {
            var builder = ImmutableArray.CreateBuilder<IPropertySymbol>();

            if (symbol.BaseType != null && symbol.BaseType.SpecialType != SpecialType.System_Object)
            {
                builder.AddRange(GetProperties(symbol.BaseType));
            }

            foreach (var member in symbol.GetMembers())
            {
                if (!member.IsStatic
                    && member.DeclaredAccessibility == Accessibility.Public
                    && member is IPropertySymbol prop)
                {
                    builder.Add(prop);
                }
            }

            return builder.ToImmutable();
        }
    }
}