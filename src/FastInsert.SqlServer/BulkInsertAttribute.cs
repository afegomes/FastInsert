using System;

namespace FastInsert.SqlServer
{
    [AttributeUsage(AttributeTargets.Class)]
    public class BulkInsertAttribute : Attribute
    {
        public BulkInsertAttribute(int batchSize = 10_000)
        {
            BatchSize = batchSize;
        }

        public int BatchSize { get; set; }
    }
}