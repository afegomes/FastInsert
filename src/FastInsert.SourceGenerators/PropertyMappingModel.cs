using System;
using Microsoft.CodeAnalysis;

namespace FastInsert.SourceGenerators
{
    internal sealed class PropertyMappingModel : IEquatable<PropertyMappingModel>
    {
        public int Index { get; }

        public string PropertyName { get; }

        public string ColumnName { get; }

        public string TypeFullName { get; }

        public SpecialType SpecialType { get; }

        public bool IsNonNullableValueType { get; }

        public PropertyMappingModel(
            int index,
            string propertyName,
            string columnName,
            string typeFullName,
            SpecialType specialType,
            bool isNonNullableValueType)
        {
            Index = index;
            PropertyName = propertyName ?? string.Empty;
            ColumnName = columnName ?? string.Empty;
            TypeFullName = typeFullName ?? string.Empty;
            SpecialType = specialType;
            IsNonNullableValueType = isNonNullableValueType;
        }

        public bool Equals(PropertyMappingModel other)
        {
            if (other is null)
            {
                return false;
            }

            if (ReferenceEquals(this, other))
            {
                return true;
            }

            return Index == other.Index
                   && SpecialType == other.SpecialType
                   && IsNonNullableValueType == other.IsNonNullableValueType
                   && string.Equals(PropertyName, other.PropertyName, StringComparison.Ordinal)
                   && string.Equals(ColumnName, other.ColumnName, StringComparison.Ordinal)
                   && string.Equals(TypeFullName, other.TypeFullName, StringComparison.Ordinal);
        }

        public override bool Equals(object obj) => obj is PropertyMappingModel other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = 17;
                hash = hash * 31 + Index;
                hash = hash * 31 + (int)SpecialType;
                hash = hash * 31 + (IsNonNullableValueType ? 1 : 0);
                hash = hash * 31 + StringComparer.Ordinal.GetHashCode(PropertyName);
                hash = hash * 31 + StringComparer.Ordinal.GetHashCode(ColumnName);
                hash = hash * 31 + StringComparer.Ordinal.GetHashCode(TypeFullName);
                return hash;
            }
        }

        public static bool operator ==(PropertyMappingModel left, PropertyMappingModel right) => Equals(left, right);

        public static bool operator !=(PropertyMappingModel left, PropertyMappingModel right) => !Equals(left, right);
    }
}