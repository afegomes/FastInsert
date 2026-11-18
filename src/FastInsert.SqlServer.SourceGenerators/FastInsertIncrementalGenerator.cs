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
            var batchSize = 10000;

            if (bulkInsertAttr?.ConstructorArguments.Length > 0 &&
                bulkInsertAttr.ConstructorArguments[0].Value is int size && size > 0)
            {
                batchSize = size;
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
                    prop.Type.IsValueType
                    && prop.Type.OriginalDefinition.SpecialType != SpecialType.System_Nullable_T
                    && prop.Type.NullableAnnotation != NullableAnnotation.Annotated));
            }

            return new EntityModel(
                entitySymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                GetWriterName(entitySymbol),
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

                writer.WriteLine($"protected override System.Data.IDataReader CreateDataReader(IReadOnlyList<{model.EntityFullName}> data)");
                writer.WriteLine("{");
                writer.Indent++;
                writer.WriteLine("return new InternalDataReader(data);");
                writer.Indent--;
                writer.WriteLine("}");
                writer.WriteLine();

                WriteConfigureMappingsMethod(writer, model.Mappings);
                WriteInternalDataReaderClass(writer, model);

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

        private static void WriteConfigureMappingsMethod(IndentedTextWriter writer, EquatableArray<PropertyMappingModel> mappings)
        {
            writer.WriteLine("protected override void ConfigureMappings(Microsoft.Data.SqlClient.SqlBulkCopy sqlBulkCopy)");
            writer.WriteLine("{");
            writer.Indent++;

            foreach (var mapping in mappings)
            {
                writer.WriteLine($"sqlBulkCopy.ColumnMappings.Add({mapping.Index}, \"{mapping.ColumnName}\");");
            }

            writer.Indent--;
            writer.WriteLine("}");
            writer.WriteLine();
        }

        private static void WriteInternalDataReaderClass(IndentedTextWriter writer, EntityModel model)
        {
            writer.WriteLine($"private sealed class InternalDataReader : FastInsert.Core.DataReaderBase");
            writer.WriteLine("{");
            writer.Indent++;

            writer.WriteLine("private readonly int _rowCount;");
            writer.WriteLine($"private readonly IReadOnlyList<{model.EntityFullName}> _data;");
            writer.WriteLine();
            writer.WriteLine("private int _currentIndex = -1;");
            writer.WriteLine($"private {model.EntityFullName} _current;");
            writer.WriteLine();

            writer.WriteLine($"public InternalDataReader(IReadOnlyList<{model.EntityFullName}> data) : base({model.Mappings.Length})");
            writer.WriteLine("{");
            writer.Indent++;
            writer.WriteLine("_rowCount = data.Count;");
            writer.WriteLine("_data = data;");
            writer.Indent--;
            writer.WriteLine("}");
            writer.WriteLine();

            writer.WriteLine("public override bool HasRows => _rowCount > 0;");
            writer.WriteLine();

            WriteReadMethod(writer);
            WriteIsDBNullMethod(writer, model.Mappings);
            WriteGetValuesMethod(writer, model.Mappings);
            WriteTypedGetters(writer, model.Mappings);
            WriteGetValueMethod(writer, model.Mappings);

            writer.Indent--;
            writer.WriteLine("}");
        }

        private static void WriteReadMethod(IndentedTextWriter writer)
        {
            writer.WriteLine("public override bool Read()");
            writer.WriteLine("{");
            writer.Indent++;

            writer.WriteLine("if (++_currentIndex < _rowCount)");
            writer.WriteLine("{");
            writer.Indent++;
            writer.WriteLine("_current = _data[_currentIndex];");
            writer.WriteLine("return true;");
            writer.Indent--;
            writer.WriteLine("}");
            writer.WriteLine();
            writer.WriteLine("_current = null;");
            writer.WriteLine("return false;");

            writer.Indent--;
            writer.WriteLine("}");
            writer.WriteLine();
        }

        private static void WriteIsDBNullMethod(IndentedTextWriter writer, EquatableArray<PropertyMappingModel> mappings)
        {
            writer.WriteLine("[System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]");
            writer.WriteLine("public override bool IsDBNull(int i)");
            writer.WriteLine("{");
            writer.Indent++;

            var nullableMappings = mappings.Where(m => !m.IsNonNullableValueType).ToArray();

            if (nullableMappings.Length == 0)
            {
                writer.WriteLine($"if (i >= 0 && i < {mappings.Length})");
                writer.WriteLine("{");
                writer.Indent++;
                writer.WriteLine("return false;");
                writer.Indent--;
                writer.WriteLine("}");
                writer.WriteLine();
                writer.WriteLine("throw new System.IndexOutOfRangeException();");
            }
            else if (nullableMappings.Length == 1 && mappings.Length > 1)
            {
                var nullable = nullableMappings[0];
                writer.WriteLine($"if (i == {nullable.Index})");
                writer.WriteLine("{");
                writer.Indent++;
                writer.WriteLine($"return _current.{nullable.PropertyName} == null;");
                writer.Indent--;
                writer.WriteLine("}");
                writer.WriteLine();
                writer.WriteLine($"if (i >= 0 && i < {mappings.Length})");
                writer.WriteLine("{");
                writer.Indent++;
                writer.WriteLine("return false;");
                writer.Indent--;
                writer.WriteLine("}");
                writer.WriteLine();
                writer.WriteLine("throw new System.IndexOutOfRangeException();");
            }
            else
            {
                writer.WriteLine("switch (i)");
                writer.WriteLine("{");
                writer.Indent++;

                foreach (var mapping in nullableMappings)
                {
                    writer.WriteLine($"case {mapping.Index}:");
                    writer.Indent++;
                    writer.WriteLine($"return _current.{mapping.PropertyName} == null;");
                    writer.Indent--;
                }

                writer.WriteLine("default:");
                writer.Indent++;
                if (nullableMappings.Length < mappings.Length)
                {
                    writer.WriteLine($"if (i >= 0 && i < {mappings.Length})");
                    writer.WriteLine("{");
                    writer.Indent++;
                    writer.WriteLine("return false;");
                    writer.Indent--;
                    writer.WriteLine("}");
                    writer.WriteLine();
                }
                writer.WriteLine("throw new System.IndexOutOfRangeException();");
                writer.Indent--;

                writer.Indent--;
                writer.WriteLine("}");
            }

            writer.Indent--;
            writer.WriteLine("}");
            writer.WriteLine();
        }

        private static void WriteGetValuesMethod(IndentedTextWriter writer, EquatableArray<PropertyMappingModel> mappings)
        {
            writer.WriteLine("public override int GetValues(object[] values)");
            writer.WriteLine("{");
            writer.Indent++;

            writer.WriteLine($"int count = System.Math.Min(values.Length, {mappings.Length});");

            foreach (var mapping in mappings)
            {
                if (mapping.IsNonNullableValueType)
                {
                    writer.WriteLine($"if (count > {mapping.Index}) values[{mapping.Index}] = _current.{mapping.PropertyName};");
                }
                else
                {
                    writer.WriteLine($"if (count > {mapping.Index}) values[{mapping.Index}] = (object)_current.{mapping.PropertyName} ?? System.DBNull.Value;");
                }
            }

            writer.WriteLine("return count;");

            writer.Indent--;
            writer.WriteLine("}");
            writer.WriteLine();
        }

        private static void WriteTypedGetters(IndentedTextWriter writer, EquatableArray<PropertyMappingModel> mappings)
        {
            var getterDefinitions = new (string MethodName, string ReturnType, System.Func<PropertyMappingModel, bool> Predicate)[]
            {
                ("GetBoolean", "bool", m => m.SpecialType == SpecialType.System_Boolean),
                ("GetByte", "byte", m => m.SpecialType == SpecialType.System_Byte),
                ("GetInt16", "short", m => m.SpecialType == SpecialType.System_Int16),
                ("GetInt32", "int", m => m.SpecialType == SpecialType.System_Int32),
                ("GetInt64", "long", m => m.SpecialType == SpecialType.System_Int64),
                ("GetDecimal", "decimal", m => m.SpecialType == SpecialType.System_Decimal),
                ("GetFloat", "float", m => m.SpecialType == SpecialType.System_Single),
                ("GetDouble", "double", m => m.SpecialType == SpecialType.System_Double),
                ("GetChar", "char", m => m.SpecialType == SpecialType.System_Char),
                ("GetString", "string", m => m.SpecialType == SpecialType.System_String),
                ("GetGuid", "System.Guid", m => m.TypeFullName == "global::System.Guid" || m.TypeFullName == "System.Guid"),
                ("GetDateTime", "System.DateTime", m => m.SpecialType == SpecialType.System_DateTime || m.TypeFullName == "global::System.DateTime" || m.TypeFullName == "System.DateTime")
            };

            foreach (var def in getterDefinitions)
            {
                var matching = mappings.Where(def.Predicate).ToArray();
                if (matching.Length == 0)
                {
                    continue;
                }

                if (matching.Length == 1)
                {
                    writer.WriteLine($"public override {def.ReturnType} {def.MethodName}(int i) => i == {matching[0].Index} ? _current.{matching[0].PropertyName} : throw new System.IndexOutOfRangeException();");
                }
                else
                {
                    writer.WriteLine($"public override {def.ReturnType} {def.MethodName}(int i)");
                    writer.WriteLine("{");
                    writer.Indent++;
                    writer.WriteLine("switch (i)");
                    writer.WriteLine("{");
                    writer.Indent++;
                    foreach (var m in matching)
                    {
                        writer.WriteLine($"case {m.Index}: return _current.{m.PropertyName};");
                    }
                    writer.WriteLine("default: throw new System.IndexOutOfRangeException();");
                    writer.Indent--;
                    writer.WriteLine("}");
                    writer.Indent--;
                    writer.WriteLine("}");
                }
            }

            writer.WriteLine();
        }

        private static void WriteGetValueMethod(IndentedTextWriter writer, EquatableArray<PropertyMappingModel> mappings)
        {
            writer.WriteLine("public override object GetValue(int i)");
            writer.WriteLine("{");
            writer.Indent++;

            writer.WriteLine("switch (i)");
            writer.WriteLine("{");
            writer.Indent++;

            foreach (var mapping in mappings)
            {
                if (mapping.IsNonNullableValueType)
                {
                    writer.WriteLine($"case {mapping.Index}: return _current.{mapping.PropertyName};");
                }
                else
                {
                    writer.WriteLine($"case {mapping.Index}: return (object)_current.{mapping.PropertyName} ?? System.DBNull.Value;");
                }
            }

            writer.WriteLine("default: throw new System.IndexOutOfRangeException();");
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