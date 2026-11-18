using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace FastInsert.SqlServer.SourceGenerators
{
    public readonly struct EquatableArray<T> : IEquatable<EquatableArray<T>>, IEnumerable<T> where T : IEquatable<T>
    {
        private readonly ImmutableArray<T> _array;

        public EquatableArray(ImmutableArray<T> array) => _array = array;

        public int Length => _array.IsDefault ? 0 : _array.Length;

        public bool Equals(EquatableArray<T> other) => _array.SequenceEqual(other._array);

        public override bool Equals(object obj) => obj is EquatableArray<T> other && Equals(other);

        public override int GetHashCode()
        {
            if (_array.IsDefault) return 0;
            var hash = 17;

            for (var index = 0; index < _array.Length; index++)
            {
                var item = _array[index];
                hash = hash * 31 + (item?.GetHashCode() ?? 0);
            }

            return hash;
        }

        public IEnumerator<T> GetEnumerator() =>
            ((IEnumerable<T>)(_array.IsDefault ? ImmutableArray<T>.Empty : _array))
            .GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}