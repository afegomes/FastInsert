using System.Collections.Generic;
using System.Data;
using System.Threading;
using System.Threading.Tasks;
using FastInsert.Core;
using Microsoft.Data.SqlClient;

namespace FastInsert.SqlServer
{
    public abstract class SqlServerWriter<T> : IDataWriter<T>
    {
        private readonly int _batchSize;
        private readonly SqlConnection _connection;

        protected SqlServerWriter(int batchSize, SqlConnection connection)
        {
            _batchSize = batchSize;
            _connection = connection;
        }

        public async Task WriteAsync(IEnumerable<T> data, CancellationToken cancellationToken)
        {
            using var sqlBulkCopy = new SqlBulkCopy(_connection, SqlBulkCopyOptions.TableLock, null);
            sqlBulkCopy.BatchSize = _batchSize;

            ConfigureMappings(sqlBulkCopy);

            using var reader = CreateDataReader(data);

            await sqlBulkCopy.WriteToServerAsync(reader, cancellationToken);
        }

        protected abstract IDataReader CreateDataReader(IEnumerable<T> data);

        protected abstract void ConfigureMappings(SqlBulkCopy sqlBulkCopy);
    }
}