using System;

namespace FastInsert.SqlServer.SourceGenerators
{
    internal sealed class EntityModel : IEquatable<EntityModel>
    {
        public string EntityFullName { get; }

        public string WriterName { get; }

        public string TableName { get; }

        public int BatchSize { get; }

        public EquatableArray<PropertyMappingModel> Mappings { get; }

        public EntityModel(
            string entityFullName,
            string writerName,
            string tableName,
            int batchSize,
            EquatableArray<PropertyMappingModel> mappings)
        {
            EntityFullName = entityFullName;
            WriterName = writerName;
            TableName = tableName;
            BatchSize = batchSize;
            Mappings = mappings;
        }

        public bool Equals(EntityModel other)
        {
            if (other is null)
            {
                return false;
            }

            if (ReferenceEquals(this, other))
            {
                return true;
            }

            return EntityFullName == other.EntityFullName
                   && WriterName == other.WriterName
                   && TableName == other.TableName
                   && BatchSize == other.BatchSize
                   && Mappings.Equals(other.Mappings);
        }

        public override bool Equals(object obj) => obj is EntityModel other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = 17;
                hash = hash * 31 + (EntityFullName?.GetHashCode() ?? 0);
                hash = hash * 31 + (WriterName?.GetHashCode() ?? 0);
                hash = hash * 31 + (TableName?.GetHashCode() ?? 0);
                hash = hash * 31 + BatchSize.GetHashCode();
                hash = hash * 31 + Mappings.GetHashCode();
                return hash;
            }
        }
    }
}