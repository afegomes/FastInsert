using System;
using System.Collections.Generic;
using System.Data;

namespace FastInsert.Core
{
    public abstract class DataReaderBase<T> : IDataReader
    {
        private readonly IEnumerator<T> _iterator;

        protected DataReaderBase(int fieldCount, IEnumerable<T> data)
        {
            if (fieldCount <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(fieldCount), "Must be greater than 0");
            }

            if (data is null)
            {
                throw new ArgumentNullException(nameof(data));
            }

            FieldCount = fieldCount;

            _iterator = data.GetEnumerator();
        }

        protected T Current => _iterator.Current;

        public int Depth => 1;

        public int FieldCount { get; }

        public bool IsClosed { get; private set; }

        public int RecordsAffected => -1;

        public bool NextResult()
        {
            return false;
        }

        public bool Read()
        {
            return _iterator.MoveNext();
        }

        public void Close()
        {
            IsClosed = true;
        }

        public bool IsDBNull(int i)
        {
            return GetValue(i) is null;
        }

        public abstract object GetValue(int i);

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                _iterator.Dispose();
            }
        }

        public object this[int i] => throw new NotImplementedException();

        public object this[string name] => throw new NotImplementedException();

        public DataTable GetSchemaTable()
        {
            throw new NotImplementedException();
        }

        public string GetName(int i)
        {
            throw new NotImplementedException();
        }

        public string GetDataTypeName(int i)
        {
            throw new NotImplementedException();
        }

        public Type GetFieldType(int i)
        {
            throw new NotImplementedException();
        }

        public int GetValues(object[] values)
        {
            throw new NotImplementedException();
        }

        public int GetOrdinal(string name)
        {
            throw new NotImplementedException();
        }

        public bool GetBoolean(int i)
        {
            throw new NotImplementedException();
        }

        public byte GetByte(int i)
        {
            throw new NotImplementedException();
        }

        public long GetBytes(int i, long fieldOffset, byte[] buffer, int bufferoffset, int length)
        {
            throw new NotImplementedException();
        }

        public char GetChar(int i)
        {
            throw new NotImplementedException();
        }

        public long GetChars(int i, long fieldoffset, char[] buffer, int bufferoffset, int length)
        {
            throw new NotImplementedException();
        }

        public Guid GetGuid(int i)
        {
            throw new NotImplementedException();
        }

        public short GetInt16(int i)
        {
            throw new NotImplementedException();
        }

        public int GetInt32(int i)
        {
            throw new NotImplementedException();
        }

        public long GetInt64(int i)
        {
            throw new NotImplementedException();
        }

        public float GetFloat(int i)
        {
            throw new NotImplementedException();
        }

        public double GetDouble(int i)
        {
            throw new NotImplementedException();
        }

        public string GetString(int i)
        {
            throw new NotImplementedException();
        }

        public decimal GetDecimal(int i)
        {
            throw new NotImplementedException();
        }

        public DateTime GetDateTime(int i)
        {
            throw new NotImplementedException();
        }

        public IDataReader GetData(int i)
        {
            throw new NotImplementedException();
        }
    }
}