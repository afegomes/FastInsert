using System.Data;
using System.Reflection;
using FastInsert.SqlServer;
using FastInsert.Writers;
using FluentAssertions;
using Xunit;

namespace FastInsert.Tests;

public class DataReaderGeneratedTests
{
    private static IDataReader CreateDataReader<T>(SqlServerWriter<T> writer, IReadOnlyList<T> data)
    {
        var method = typeof(SqlServerWriter<T>)
            .GetMethod("CreateDataReader",
                BindingFlags.NonPublic | BindingFlags.Instance);

        return (IDataReader)method!.Invoke(writer, [data]);
    }

    [Fact]
    public void StandardDataReader_ShouldReturnCorrectValues_AndTypedGetters()
    {
        var item = new Standard
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
            SomeString = "Test String",
            SomeGuid = Guid.Parse("70daaf11-392f-4f1d-9d95-111981d26bf2"),
            SomeTimeSpan = TimeSpan.FromSeconds(12),
            SomeDateTime = new DateTime(2023, 07, 29, 15, 43, 20, DateTimeKind.Utc)
        };

        var writer = new StandardWriter(null);
        using var reader = CreateDataReader(writer, [item]);

        reader.Read().Should().BeTrue();
        reader.FieldCount.Should().Be(16);

        // Typed getters
        reader.GetBoolean(0).Should().Be(true);
        reader.GetByte(1).Should().Be(45);
        reader.GetInt16(2).Should().Be(-50);
        reader.GetInt32(4).Should().Be(-1001);
        reader.GetInt64(6).Should().Be(-51);
        reader.GetDecimal(8).Should().Be(10.99m);
        reader.GetFloat(9).Should().Be(81.96f);
        reader.GetDouble(10).Should().Be(1.52);
        reader.GetChar(11).Should().Be('a');
        reader.GetString(12).Should().Be("Test String");
        reader.GetGuid(13).Should().Be(Guid.Parse("70daaf11-392f-4f1d-9d95-111981d26bf2"));
        reader.GetDateTime(15).Should().Be(new DateTime(2023, 07, 29, 15, 43, 20, DateTimeKind.Utc));

        // IsDBNull
        for (var i = 0; i < 16; i++)
        {
            reader.IsDBNull(i).Should().BeFalse();
        }

        // GetValue
        reader.GetValue(0).Should().Be(true);
        reader.GetValue(1).Should().Be((byte)45);
        reader.GetValue(2).Should().Be((short)-50);
        reader.GetValue(3).Should().Be((ushort)100);
        reader.GetValue(4).Should().Be(-1001);
        reader.GetValue(5).Should().Be((uint)23);
        reader.GetValue(6).Should().Be(-51L);
        reader.GetValue(7).Should().Be(617UL);
        reader.GetValue(8).Should().Be(10.99m);
        reader.GetValue(9).Should().Be(81.96f);
        reader.GetValue(10).Should().Be(1.52);
        reader.GetValue(11).Should().Be('a');
        reader.GetValue(12).Should().Be("Test String");
        reader.GetValue(13).Should().Be(Guid.Parse("70daaf11-392f-4f1d-9d95-111981d26bf2"));
        reader.GetValue(14).Should().Be(TimeSpan.FromSeconds(12));
        reader.GetValue(15).Should().Be(new DateTime(2023, 07, 29, 15, 43, 20, DateTimeKind.Utc));

        // GetValues
        var values = new object[16];
        var populated = reader.GetValues(values);
        populated.Should().Be(16);
        values[0].Should().Be(true);
        values[1].Should().Be((byte)45);
        values[12].Should().Be("Test String");
        values[15].Should().Be(new DateTime(2023, 07, 29, 15, 43, 20, DateTimeKind.Utc));
    }

    [Fact]
    public void StandardDataReader_NullString_ShouldHandleDBNull()
    {
        var item = new Standard
        {
            SomeString = null
        };

        var writer = new StandardWriter(null);
        using var reader = CreateDataReader(writer, [item]);

        reader.Read().Should().BeTrue();

        reader.IsDBNull(12).Should().BeTrue();
        reader.GetValue(12).Should().Be(DBNull.Value);

        var values = new object[16];
        reader.GetValues(values);
        values[12].Should().Be(DBNull.Value);
    }

    [Fact]
    public void StandardDataReader_OutOfBounds_ShouldThrow()
    {
        var item = new Standard();
        var writer = new StandardWriter(null);
        using var reader = CreateDataReader(writer, [item]);

        reader.Read().Should().BeTrue();

        reader.Invoking(r => r.IsDBNull(-1)).Should().Throw<IndexOutOfRangeException>();
        reader.Invoking(r => r.IsDBNull(16)).Should().Throw<IndexOutOfRangeException>();
        reader.Invoking(r => r.GetValue(-1)).Should().Throw<IndexOutOfRangeException>();
        reader.Invoking(r => r.GetValue(16)).Should().Throw<IndexOutOfRangeException>();
        reader.Invoking(r => r.GetBoolean(1)).Should().Throw<IndexOutOfRangeException>();
        reader.Invoking(r => r.GetInt32(0)).Should().Throw<IndexOutOfRangeException>();
    }

    [Fact]
    public void CustomizedDataReader_SingleString_ShouldWork()
    {
        var item = new Customized
        {
            SomeString = "Custom Value"
        };

        var writer = new CustomizedWriter(null);
        using var reader = CreateDataReader(writer, [item]);

        reader.Read().Should().BeTrue();
        reader.FieldCount.Should().Be(1);

        reader.IsDBNull(0).Should().BeFalse();
        reader.GetString(0).Should().Be("Custom Value");
        reader.GetValue(0).Should().Be("Custom Value");

        var values = new object[1];
        reader.GetValues(values).Should().Be(1);
        values[0].Should().Be("Custom Value");
    }

    [Fact]
    public void MultiTypeEntity_ShouldHandleMultipleTypesAndNullables()
    {
        var item = new MultiTypeEntity
        {
            Id = 1,
            Age = 30,
            Name = "John Doe",
            Description = null,
            Score = null,
            Key = Guid.Parse("11111111-2222-3333-4444-555555555555"),
            SecondaryKey = null,
            Active = true,
            Deleted = false
        };

        var writer = new MultiTypeEntityWriter(null);
        using var reader = CreateDataReader(writer, [item]);

        reader.Read().Should().BeTrue();
        reader.FieldCount.Should().Be(9);

        // Multiple ints
        reader.GetInt32(0).Should().Be(1);
        reader.GetInt32(1).Should().Be(30);
        reader.Invoking(r => r.GetInt32(2)).Should().Throw<IndexOutOfRangeException>();

        // Multiple bools
        reader.GetBoolean(7).Should().Be(true);
        reader.GetBoolean(8).Should().Be(false);

        // String
        reader.GetString(2).Should().Be("John Doe");

        // Guid
        reader.GetGuid(5).Should().Be(Guid.Parse("11111111-2222-3333-4444-555555555555"));

        // IsDBNull
        reader.IsDBNull(0).Should().BeFalse(); // Id
        reader.IsDBNull(1).Should().BeFalse(); // Age
        reader.IsDBNull(2).Should().BeFalse(); // Name
        reader.IsDBNull(3).Should().BeTrue(); // Description
        reader.IsDBNull(4).Should().BeTrue(); // Score
        reader.IsDBNull(5).Should().BeFalse(); // Key
        reader.IsDBNull(6).Should().BeTrue(); // SecondaryKey
        reader.IsDBNull(7).Should().BeFalse(); // Active
        reader.IsDBNull(8).Should().BeFalse(); // Deleted

        // GetValue
        reader.GetValue(0).Should().Be(1);
        reader.GetValue(1).Should().Be(30);
        reader.GetValue(2).Should().Be("John Doe");
        reader.GetValue(3).Should().Be(DBNull.Value);
        reader.GetValue(4).Should().Be(DBNull.Value);
        reader.GetValue(5).Should().Be(Guid.Parse("11111111-2222-3333-4444-555555555555"));
        reader.GetValue(6).Should().Be(DBNull.Value);
        reader.GetValue(7).Should().Be(true);
        reader.GetValue(8).Should().Be(false);

        // GetValues
        var values = new object[9];
        reader.GetValues(values).Should().Be(9);
        values[0].Should().Be(1);
        values[2].Should().Be("John Doe");
        values[3].Should().Be(DBNull.Value);
        values[4].Should().Be(DBNull.Value);
        values[6].Should().Be(DBNull.Value);
    }
}

[BulkInsert]
public class MultiTypeEntity
{
    public int Id { get; init; }

    public int Age { get; init; }

    public string Name { get; init; }

    public string Description { get; init; }

    public int? Score { get; init; }

    public Guid Key { get; init; }

    public Guid? SecondaryKey { get; init; }

    public bool Active { get; init; }

    public bool Deleted { get; init; }
}