using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace FastInsert.SqlServer
{
    public interface IDataWriter<in T>
    {
        Task WriteAsync(string tableName, IReadOnlyList<T> data, CancellationToken cancellationToken);
    }
}