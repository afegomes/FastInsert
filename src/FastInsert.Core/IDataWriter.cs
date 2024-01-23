using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace FastInsert.Core
{
    public interface IDataWriter<in T>
    {
        Task WriteAsync(IEnumerable<T> data, CancellationToken cancellationToken);
    }
}