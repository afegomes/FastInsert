namespace FastInsert.SqlServer.SourceGenerators
{
    internal sealed class DiEntry
    {
        public DiEntry(string entityFullName, string writerFullName)
        {
            EntityFullName = entityFullName;
            WriterFullName = writerFullName;
        }

        public string EntityFullName { get; set; }

        public string WriterFullName { get; set; }
    }
}