using System;

namespace FastInsert.Core
{
    [AttributeUsage(AttributeTargets.Class)]
    public class BulkInsertAttribute : Attribute
    {
        public BulkInsertAttribute(int batchSize = 1000)
        {
            BatchSize = batchSize;
        }

        public int BatchSize { get; set; }
    }
}