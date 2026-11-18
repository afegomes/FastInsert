using System;
using System.Collections;
using System.Data.Common;

namespace FastInsert.Core
{
    public abstract class DataReaderBase : DbDataReader
    {
        private bool _closed;

        protected DataReaderBase(int fieldCount)
        {
            FieldCount = fieldCount > 0
                ? fieldCount
                : throw new ArgumentOutOfRangeException(nameof(fieldCount), "Must be greater than 0");
        }

        public override int Depth => 1;

        public override int FieldCount { get; }

        public override bool IsClosed => _closed;

        public override int RecordsAffected => -1;

        public override bool NextResult() => false;

        public override void Close() => _closed = true;

        public override string GetName(int i) => throw new NotImplementedException();

        public override string GetDataTypeName(int i) => throw new NotImplementedException();

        public override Type GetFieldType(int i) => throw new NotImplementedException();

        public override int GetOrdinal(string name) => throw new NotImplementedException();

        public override bool GetBoolean(int i) => throw new NotImplementedException();

        public override byte GetByte(int i) => throw new NotImplementedException();

        public override long GetBytes(int i, long fieldOffset, byte[] buffer, int bufferoffset, int length) =>
            throw new NotImplementedException();

        public override char GetChar(int i) => throw new NotImplementedException();

        public override long GetChars(int i, long fieldoffset, char[] buffer, int bufferoffset, int length) =>
            throw new NotImplementedException();

        public override Guid GetGuid(int i) => throw new NotImplementedException();

        public override short GetInt16(int i) => throw new NotImplementedException();

        public override int GetInt32(int i) => throw new NotImplementedException();

        public override long GetInt64(int i) => throw new NotImplementedException();

        public override float GetFloat(int i) => throw new NotImplementedException();

        public override double GetDouble(int i) => throw new NotImplementedException();

        public override string GetString(int i) => throw new NotImplementedException();

        public override decimal GetDecimal(int i) => throw new NotImplementedException();

        public override DateTime GetDateTime(int i) => throw new NotImplementedException();

        public override object this[int ordinal] => throw new NotImplementedException();

        public override object this[string name] => throw new NotImplementedException();

        public override IEnumerator GetEnumerator() => throw new NotImplementedException();
    }
}