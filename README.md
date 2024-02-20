FastInsert.SqlServer
=======

![Nuget](https://img.shields.io/nuget/v/FastInsert.SqlServer)

FastInsert is a .NET library to bulk insert objects into SQL Server tables.

It uses Source Generators to generate the conversion code during the build instead of using reflection during runtime.

### Prerequisites

The app needs to target .NET Standard 2.1 or any .NET implementation of it.

### Installation

Use the .NET CLI to install the package and its dependencies:

   ```sh
   dotnet add package FastInsert.SqlServer
   ```

### Usage

The classes targeted for conversion should have the `BulkInsert` attribute:

   ```csharp
   using FastInsert.Core;
   
   namespace MyProject;
   
   [BulkInsert]
   public class Customer
   {
   }
   ```

The batch size will be 1000 by default, but it can be customized:

   ```csharp
   using FastInsert.Core;
   
   namespace MyProject;
   
   [BulkInsert(5000)]
   public class Customer
   {
   }
   ```

By default, the class will be mapped to a table with the same name, but this can be overridden using
the `System.ComponentModel.DataAnnotations.Schema.TableAttribute` attribute:

   ```csharp
   [Table("Customers")]
   public class Customer
   {
   }
   ```

Also by default, the properties will be mapped to columns with the same name. The name can be overridden using
the `System.ComponentModel.DataAnnotations.Schema.ColumnAttribute` attribute:

   ```csharp
   public class Customer
   {
       [Column("phone_number")]
       public string PhoneNumber { get; set; }
   }
   ```

To insert the data, you need to inject `FastInsert.Core.IDataWriter<T>`:

   ```csharp
   public class CustomerService(IDataWriter<Customer> dataWriter)
   {
       public Task SaveCustomers(IEnumerable<Customer> customers, CancellationToken cancellationToken)
       {
           return dataWriter.WriteAsync(customers, cancellationToken);
       }
   }
   ```

You can configure dependency injection by calling an extension method:

   ```csharp
   using FastInsert.DependencyInjection;
   
   var builder = Host.CreateApplicationBuilder(args);
   
   builder.Services.AddFastInsert();
   ```

## Contributing

Pull requests are welcome, but please open an issue first to discuss your idea.

## License

Distributed under the GPL-3.0 License. See `LICENSE` for more information.

## Changelog

### 1.1.0 (2024-02-20)

Add support for nested classes

### 1.0.0 (2024-01-23)

First release
