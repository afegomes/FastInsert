using System.ComponentModel.DataAnnotations.Schema;
using Dapper;
using FastInsert.Core;
using FastInsert.Writers;
using FluentAssertions;
using Xunit;

namespace FastInsert.Tests;

public class DataWriterTest(DatabaseFixture database) : IClassFixture<DatabaseFixture>
{
    [Fact]
    public async Task ShouldCreateDataTableUsingDefaultNames()
    {
        // Arrange
        var data = new List<Standard>
        {
            new()
            {
                SomeBoolean = true,
                SomeByte = 45,
                SomeShort = -50,
                SomeUnsignedShort = 100,
                SomeInt = -1001,
                SomeUnsignedInt = 23,
                SomeLong = -51,
                SomeUnsignedLong = 617,
                SomeDecimal = 10.99m,
                SomeFloat = 81.96f,
                SomeDouble = 1.52,
                SomeChar = 'a',
                SomeString = "All your base are belong yo us",
                SomeGuid = Guid.Parse("70daaf11-392f-4f1d-9d95-111981d26bf2"),
                SomeTimeSpan = TimeSpan.FromSeconds(12),
                SomeDateTime = new DateTime(2023, 07, 29, 15, 43, 20, DateTimeKind.Utc)
            }
        };

        var writer = new StandardWriter(database.Connection);

        // Act
        await writer.WriteAsync(data, CancellationToken.None);

        // Assert
        var result = await database.Connection.QueryAsync<Standard>("SELECT * FROM Standard");

        result.Should().BeEquivalentTo(data);
    }

    [Fact]
    public async Task ShouldCreateDataTableUsingCustomNames()
    {
        // Arrange
        var data = new List<Customized>
        {
            new()
            {
                SomeString = "You can't hold no groove if you ain't got no pocket"
            }
        };

        var writer = new CustomizedWriter(database.Connection);

        // Act
        await writer.WriteAsync(data, CancellationToken.None);

        // Assert
        var result = await database.Connection.QuerySingleAsync("SELECT * FROM CustomTable");

        Assert.Equal(data[0].SomeString, result.CustomColumn);
    }
}

[BulkInsert]
public class Standard
{
    public bool SomeBoolean { get; init; }

    public byte SomeByte { get; init; }

    public short SomeShort { get; init; }

    public ushort SomeUnsignedShort { get; init; }

    public int SomeInt { get; init; }

    public uint SomeUnsignedInt { get; init; }

    public long SomeLong { get; init; }

    public ulong SomeUnsignedLong { get; init; }

    public decimal SomeDecimal { get; init; }

    public float SomeFloat { get; init; }

    public double SomeDouble { get; init; }

    public char SomeChar { get; init; }

    public string SomeString { get; init; }

    public Guid SomeGuid { get; init; }

    public TimeSpan SomeTimeSpan { get; init; }

    public DateTime SomeDateTime { get; init; }
}

[BulkInsert(100)]
[Table("CustomTable")]
public class Customized
{
    [Column("CustomColumn")]
    public string SomeString { get; init; }
}