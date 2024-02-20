using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
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
            var entities = context.SyntaxProvider
                .CreateSyntaxProvider(IsCandidate, GetEntityTypeDeclarationSyntax)
                .Where(type => type != null)
                .Collect();

            var combined = context.CompilationProvider.Combine(entities);

            context.RegisterSourceOutput(combined, (ctx, source) => GenerateSources(source.Left, source.Right, ctx));
        }

        private static bool IsCandidate(SyntaxNode node, CancellationToken cancellation)
        {
            cancellation.ThrowIfCancellationRequested();

            return node switch
            {
                ClassDeclarationSyntax classDeclarationSyntax => classDeclarationSyntax.AttributeLists.Count > 0,
                StructDeclarationSyntax structDeclarationSyntax => structDeclarationSyntax.AttributeLists.Count > 0,
                _ => false
            };
        }

        private static TypeDeclarationSyntax GetEntityTypeDeclarationSyntax(GeneratorSyntaxContext ctx, CancellationToken cancellation)
        {
            var declaration = (TypeDeclarationSyntax)ctx.Node;

            foreach (var list in declaration.AttributeLists)
            {
                foreach (var attributeSyntax in list.Attributes)
                {
                    cancellation.ThrowIfCancellationRequested();

                    if (!(ctx.SemanticModel.GetSymbolInfo(attributeSyntax).Symbol is IMethodSymbol attributeSymbol))
                    {
                        continue;
                    }

                    var fullName = attributeSymbol.ContainingType.ToDisplayString();

                    if (fullName == BulkInsertAttribute)
                    {
                        return declaration;
                    }
                }
            }

            return null;
        }

        private static void GenerateSources(Compilation compilation, ImmutableArray<TypeDeclarationSyntax> classes, SourceProductionContext ctx)
        {
            if (classes.IsDefaultOrEmpty)
            {
                return;
            }

            var names = new Dictionary<string, string>();

            var bulkInsertSymbol = compilation.GetTypeByMetadataName(BulkInsertAttribute);
            var columnSymbol = compilation.GetTypeByMetadataName(ColumnAttribute);
            var tableSymbol = compilation.GetTypeByMetadataName(TableAttribute);

            foreach (var typeDeclarationSyntax in classes)
            {
                ctx.CancellationToken.ThrowIfCancellationRequested();

                var model = compilation.GetSemanticModel(typeDeclarationSyntax.SyntaxTree);

                if (!(model.GetDeclaredSymbol(typeDeclarationSyntax) is INamedTypeSymbol entitySymbol))
                {
                    continue;
                }

                var entityName = GetFullType(entitySymbol);
                var tableName = GetTableName(tableSymbol, entitySymbol);
                var batchSize = GetBatchSize(bulkInsertSymbol, entitySymbol);

                var writerName = $"{entitySymbol.Name}Writer";

                var properties = GetProperties(entitySymbol);
                var mappings = new List<MappingConfiguration>(properties.Count);

                for (var i = 0; i < properties.Count; i++)
                {
                    var property = properties[i];
                    var columnName = GetColumnName(columnSymbol, property);

                    var config = new MappingConfiguration
                    {
                        Index = i,
                        ColumnName = columnName,
                        PropertyName = property.Name
                    };

                    mappings.Add(config);
                }

                ctx.AddSource($"{writerName}.g.cs", GenerateDataWriter(entityName, writerName, tableName, batchSize, mappings));

                names.Add(entityName, $"FastInsert.Writers.{writerName}");
            }

            ctx.AddSource("DependencyInjectionExtensions.g.cs", GenerateDependencyInjectionExtensions(names));
        }

        private static string GenerateDataWriter(string entityName, string writerName, string tableName, int batchSize, List<MappingConfiguration> mappings)
        {
            var source = new StringBuilder();

            using (var writer = new IndentedTextWriter(new StringWriter(source), "\t"))
            {
                writer.WriteLine("// Auto-generated code");
                writer.WriteLine("namespace FastInsert.Writers");
                writer.WriteLine("{");
                writer.Indent++;

                writer.WriteLine($"internal sealed class {writerName} : FastInsert.SqlServer.SqlServerWriter<{entityName}>");
                writer.WriteLine("{");
                writer.Indent++;

                writer.WriteLine($"public {writerName}(Microsoft.Data.SqlClient.SqlConnection connection) : base({batchSize}, connection)");
                writer.WriteLine("{");
                writer.WriteLine("}");
                writer.WriteLine();

                writer.WriteLine($"protected override System.Data.IDataReader CreateDataReader(IEnumerable<{entityName}> data)");
                writer.WriteLine("{");
                writer.Indent++;
                writer.WriteLine("return new InternalDataReader(data);");
                writer.Indent--;
                writer.WriteLine("}");
                writer.WriteLine();

                WriteConfigureMappingsMethod(writer, tableName, mappings);

                writer.WriteLine($"private sealed class InternalDataReader(IEnumerable<{entityName}> data) : FastInsert.Core.DataReaderBase<{entityName}>({mappings.Count}, data)");
                writer.WriteLine("{");
                writer.Indent++;
                WriteGetValueMethod(writer, mappings);
                writer.Indent--;
                writer.WriteLine("}");

                writer.Indent--;
                writer.WriteLine("}");

                writer.Indent--;
                writer.WriteLine("}");
            }

            return source.ToString();
        }

        private static string GenerateDependencyInjectionExtensions(Dictionary<string, string> names)
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

                foreach (var entry in names)
                {
                    writer.WriteLine($"services.AddScoped<FastInsert.Core.IDataWriter<{entry.Key}>, {entry.Value}>();");
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

        private static void WriteConfigureMappingsMethod(IndentedTextWriter writer, string tableName, List<MappingConfiguration> mappings)
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

        private static void WriteGetValueMethod(IndentedTextWriter writer, List<MappingConfiguration> mappings)
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

        private static string GetFullType(ISymbol symbol)
        {
            var name = symbol.Name;
            var parent = symbol;

            while (true)
            {
                parent = parent.ContainingNamespace;

                if (parent is null || string.IsNullOrEmpty(parent.Name))
                {
                    break;
                }

                name = $"{parent.Name}.{name}";
            }

            return name;
        }

        private static int GetBatchSize(ISymbol attributeSymbol, ISymbol classSymbol)
        {
            foreach (var attributeData in classSymbol.GetAttributes())
            {
                if (!attributeSymbol.Equals(attributeData.AttributeClass, SymbolEqualityComparer.Default))
                {
                    continue;
                }

                if (attributeData.ConstructorArguments.Length == 0)
                {
                    break;
                }

                var value = attributeData.ConstructorArguments[0].Value;

                if (value is null)
                {
                    break;
                }

                var batchSize = (int)value;

                if (batchSize < 1)
                {
                    break;
                }

                return batchSize;
            }

            throw new NotSupportedException();
        }

        private static string GetTableName(ISymbol attributeSymbol, ISymbol classSymbol)
        {
            foreach (var attributeData in classSymbol.GetAttributes())
            {
                if (!attributeSymbol.Equals(attributeData.AttributeClass, SymbolEqualityComparer.Default))
                {
                    continue;
                }

                if (attributeData.ConstructorArguments.Length == 0)
                {
                    break;
                }

                var tableName = (string)attributeData.ConstructorArguments[0].Value;

                if (string.IsNullOrEmpty(tableName))
                {
                    break;
                }

                return tableName;
            }

            return classSymbol.Name;
        }

        private static string GetColumnName(ISymbol attributeSymbol, ISymbol propertySymbol)
        {
            foreach (var attributeData in propertySymbol.GetAttributes())
            {
                if (!attributeSymbol.Equals(attributeData.AttributeClass, SymbolEqualityComparer.Default))
                {
                    continue;
                }

                var propertyName = (string)attributeData.ConstructorArguments[0].Value;

                if (string.IsNullOrEmpty(propertyName))
                {
                    break;
                }

                return propertyName;
            }

            return propertySymbol.Name;
        }

        private static List<IPropertySymbol> GetProperties(ITypeSymbol symbol)
        {
            var properties = new List<IPropertySymbol>();

            if (symbol.BaseType != null)
            {
                properties.AddRange(GetProperties(symbol.BaseType));
            }

            foreach (var member in symbol.GetMembers())
            {
                if (member.IsStatic || member.DeclaredAccessibility != Accessibility.Public || !(member is IPropertySymbol property))
                {
                    continue;
                }

                properties.Add(property);
            }

            return properties;
        }
    }

    internal sealed class MappingConfiguration
    {
        public int Index { get; set; }

        public string ColumnName { get; set; }

        public string PropertyName { get; set; }
    }
}