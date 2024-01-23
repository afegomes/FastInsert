using Microsoft.Data.SqlClient;

namespace FastInsert.Tests;

public class DatabaseFixture : IDisposable
{
    public SqlConnection Connection { get; }

    public DatabaseFixture()
    {
        const string connectionString = "Data Source=localhost;User Id=sa;Password=Dev@12345;Initial Catalog=FastInsert;TrustServerCertificate=true";

        Connection = new SqlConnection(connectionString);
        Connection.Open();

        CreateDatabase(Connection);
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!disposing)
        {
            return;
        }

        Connection?.Dispose();
    }

    private static void CreateDatabase(SqlConnection connection)
    {
        ExecuteCommand("DROP TABLE IF EXISTS Standard", connection);
        ExecuteCommand("DROP TABLE IF EXISTS CustomTable", connection);

        const string createStandard
            = """
              CREATE TABLE Standard
              (
                  Id INT NOT NULL IDENTITY(1,1) PRIMARY KEY,
                  SomeBoolean BIT NOT NULL,
                  SomeByte TINYINT NOT NULL,
                  SomeShort SMALLINT NOT NULL,
                  SomeUnsignedShort SMALLINT NOT NULL,
                  SomeInt INT NOT NULL,
                  SomeUnsignedInt INT NOT NULL,
                  SomeLong BIGINT NOT NULL,
                  SomeUnsignedLong BIGINT NOT NULL,
                  SomeDecimal DECIMAL(5,2) NOT NULL,
                  SomeFloat FLOAT NOT NULL,
                  SomeDouble DOUBLE PRECISION NOT NULL,
                  SomeChar CHAR(1) NOT NULL,
                  SomeString VARCHAR(100) NOT NULL,
                  SomeGuid UNIQUEIDENTIFIER NOT NULL,
                  SomeTimeSpan TIME NOT NULL,
                  SomeDateTime DATETIME NOT NULL
              )
              """;

        const string createCustomized
            = """
              CREATE TABLE CustomTable
              (
                  Id INT NOT NULL IDENTITY(1,1) PRIMARY KEY,
                  CustomColumn VARCHAR(100) NOT NULL
              )
              """;

        ExecuteCommand(createStandard, connection);
        ExecuteCommand(createCustomized, connection);
    }

    private static void ExecuteCommand(string sql, SqlConnection connection)
    {
        using var command = new SqlCommand(sql, connection);
        command.ExecuteNonQuery();
    }
}