﻿using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using Xunit;

namespace Nevermore.IntegrationTests
{
    public class RelationalTestsFixture : FixtureWithRelationalStore
    {
        public RelationalTestsFixture()
        {
            var mappings = new DocumentMap[]
            {
                new CustomerMap(),
                new ProductMap(),
                new LineItemMap(),
            };

            Mappings.Install(mappings);

            var output = new StringBuilder();
            using (var transaction = Store.BeginTransaction(IsolationLevel.ReadCommitted))
            {
                output.Clear();

                foreach (var map in mappings)
                    SchemaGenerator.WriteTableSchema(map, null, output);

                transaction.ExecuteScalar<int>(output.ToString());

                transaction.Commit();
            }
        }
    }

    public class RelationalStoreFixture : IClassFixture<RelationalTestsFixture>, IDisposable
    {
        private readonly RelationalTestsFixture Fixture;
        private readonly IRelationalStore Store;

        public RelationalStoreFixture(RelationalTestsFixture fixture)
        {
            Fixture = fixture;
            Store = fixture.Store;
        }

        void IDisposable.Dispose()
        {
            IntegrationTestDatabase.ExecuteScript("EXEC sp_msforeachtable \"ALTER TABLE ? NOCHECK CONSTRAINT all\"");
            IntegrationTestDatabase.ExecuteScript("EXEC sp_msforeachtable \"DELETE FROM ?\"");
            IntegrationTestDatabase.ExecuteScript("EXEC sp_msforeachtable \"ALTER TABLE ? WITH CHECK CHECK CONSTRAINT all\"");
            IntegrationTestDatabase.Store.Reset();
        }

        [Fact]
        public void ShouldGenerateIdsUnlessExplicitlyAssigned()
        {
            // The K and Id columns allow you to give records an ID, or use an auto-generated, unique ID
            using (var transaction = Store.BeginTransaction())
            {
                var customer1 = new Customer { FirstName = "Alice", LastName = "Apple", LuckyNumbers = new[] { 12, 13 }, Nickname = "Ally", Roles = { "web-server", "app-server" } };
                var customer2 = new Customer { FirstName = "Bob", LastName = "Banana", LuckyNumbers = new[] { 12, 13 }, Nickname = "B-man", Roles = { "web-server", "app-server" } };
                var customer3 = new Customer { FirstName = "Charlie", LastName = "Cherry", LuckyNumbers = new[] { 12, 13 }, Nickname = "Chazza", Roles = { "web-server", "app-server" } };
                transaction.Insert(customer1);
                transaction.Insert(customer2);
                transaction.Insert(customer3, "Customers-Chazza");

                Assert.StartsWith("Customers-", customer1.Id);
                Assert.StartsWith("Customers-", customer2.Id);
                Assert.Equal("Customers-Chazza", customer3.Id);

                transaction.Commit();
            }
        }

        [Fact]
        public void ShouldPersistReferenceCollectionsToAllowLikeSearches()
        {
            using (var transaction = Store.BeginTransaction())
            {
                var customer1 = new Customer { FirstName = "Alice", LastName = "Apple", LuckyNumbers = new[] { 12, 13 }, Nickname = "Ally", Roles = { "web-server", "app-server" } };
                var customer2 = new Customer { FirstName = "Bob", LastName = "Banana", LuckyNumbers = new[] { 12, 13 }, Nickname = "B-man", Roles = { "db-server", "app-server" } };
                var customer3 = new Customer { FirstName = "Charlie", LastName = "Cherry", LuckyNumbers = new[] { 12, 13 }, Nickname = "Chazza", Roles = { "web-server", "app-server" } };
                transaction.Insert(customer1);
                transaction.Insert(customer2);
                transaction.Insert(customer3);
                transaction.Commit();
            }

            // ReferenceCollection columns that are indexed are always stored in pipe-separated format with pipes at the front and end: |foo|bar|baz|
            using (var transaction = Store.BeginTransaction())
            {
                var customers = transaction.Query<Customer>()
                    .Where("[Roles] LIKE @role")
                    .LikeParameter("role", "web-server")
                    .ToList();
                Assert.Equal(2, customers.Count);
            }
        }

        [Fact]
        public void ShouldMultiSelect()
        {
            Fixture.InTransaction(transaction =>
            {
                transaction.Insert(new Product { Name = "Talking Elmo", Price = 100 }, "product-1");
                transaction.Insert(new Product { Name = "Lego set", Price = 200 }, "product-2");

                transaction.Insert(new LineItem { ProductId = "product-1", Name = "Line 1", Quantity = 10 });
                transaction.Insert(new LineItem { ProductId = "product-1", Name = "Line 2", Quantity = 10 });
                transaction.Insert(new LineItem { ProductId = "product-2", Name = "Line 3", Quantity = 20 });
            });

            using (var transaction = Store.BeginTransaction())
            {
                var lines = transaction.ExecuteReaderWithProjection("SELECT line.Id as line_id, line.Name as line_name, line.ProductId as line_productid, line.Json as line_json, prod.Id as prod_id, prod.Name as prod_name, prod.json as prod_json from LineItem line inner join Product prod on prod.Id = line.ProductId", new CommandParameters(), map => new
                {
                    LineItem = map.Map<LineItem>("line"),
                    Product = map.Map<Product>("prod")
                }).ToList();

                Assert.Equal(3, lines.Count);
                Assert.True(lines[0].LineItem.Name == "Line 1" && lines[0].Product.Name == "Talking Elmo" && lines[0].Product.Price == 100);
                Assert.True(lines[1].LineItem.Name == "Line 2" && lines[1].Product.Name == "Talking Elmo" && lines[1].Product.Price == 100);
                Assert.True(lines[2].LineItem.Name == "Line 3" && lines[2].Product.Name == "Lego set" && lines[2].Product.Price == 200);
            }
        }

        [Fact]
        public void ShouldShowNiceErrorIfFieldsAreTooLong()
        {
            // SQL normally thows "String or binary data would be truncated. The statement has been terminated."
            // Since we know the lengths, we show a better error first
            using (var transaction = Store.BeginTransaction())
            {
                var ex = Assert.Throws<StringTooLongException>(() => transaction.Insert(new Customer { FirstName = new string('A', 21), LastName = "Apple", LuckyNumbers = new[] { 12, 13 }, Nickname = "Ally", Roles = { "web-server", "app-server" } }));
                Assert.Equal("An attempt was made to store 21 characters in the Customer.FirstName column, which only allows 20 characters.", ex.Message);
            }
        }

        [Fact]
        public void ShouldShowFriendlyUniqueConstraintErrors()
        {
            using (var transaction = Store.BeginTransaction())
            {
                var customer1 = new Customer { FirstName = "Alice", LastName = "Apple", LuckyNumbers = new[] { 12, 13 }, Nickname = "Ally", Roles = { "web-server", "app-server" } };
                var customer2 = new Customer { FirstName = "Alice", LastName = "Appleby", LuckyNumbers = new[] { 12, 13 }, Nickname = "Ally", Roles = { "web-server", "app-server" } };
                var customer3 = new Customer { FirstName = "Alice", LastName = "Apple", LuckyNumbers = new[] { 12, 13 }, Nickname = "Ally", Roles = { "web-server", "app-server" } };

                transaction.Insert(customer1);
                transaction.Insert(customer2);
                var ex = Assert.Throws<UniqueConstraintViolationException>(() => transaction.Insert(customer3));

                Assert.Equal("Customers must have a unique name", ex.Message);
            }
        }

    }

    public class Customer
    {
        public Customer()
        {
            Roles = new HashSet<string>();
        }

        public string Id { get; private set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public HashSet<string> Roles { get; private set; }
        public string Nickname { get; set; }
        public int[] LuckyNumbers { get; set; }
        public string ApiKey { get; set; }
        public string[] Passphrases { get; set; }
    }

    public class CustomerMap : DocumentMap<Customer>
    {
        public CustomerMap()
        {
            Column(m => m.FirstName).WithMaxLength(20);
            Column(m => m.LastName);
            Column(m => m.Roles, map =>
            {
                map.ReaderWriter = new HashSetReaderWriter(map.ReaderWriter);
                map.DbType = DbType.String;
                map.MaxLength = int.MaxValue;
            });

            Unique("UniqueCustomerNames", new[] { "FirstName", "LastName" }, "Customers must have a unique name");
        }
    }

    public class LineItem
    {
        public string Id { get; private set; }
        public string Name { get; set; }
        public string ProductId { get; set; }
        public int Quantity { get; set; }
    }

    public class LineItemMap : DocumentMap<LineItem>
    {
        public LineItemMap()
        {
            Column(m => m.Name);
            Column(m => m.ProductId);
        }
    }

    public class Product
    {
        public string Id { get; private set; }
        public string Name { get; set; }
        public decimal Price { get; set; }
    }

    public class ProductMap : DocumentMap<Product>
    {
        public ProductMap()
        {
            Column(m => m.Name);
        }
    }

    public class HashSetReaderWriter : PropertyReaderWriterDecorator
    {
        public HashSetReaderWriter(IPropertyReaderWriter<object> original)
            : base(original)
        {
        }

        public override object Read(object target)
        {
            var value = base.Read(target) as HashSet<string>;
            if (value == null || value.Count == 0)
                return "";

            var items = new StringBuilder();
            items.Append("|");
            foreach (var item in value)
            {
                items.Append(item);
                items.Append("|");
            }
            return items.ToString();
        }

        public override void Write(object target, object value)
        {
            var valueAsString = (value ?? string.Empty).ToString().Split('|');

            var collection = base.Read(target) as HashSet<string>;
            if (collection == null)
            {
                base.Write(target, collection = new HashSet<string>());
            }

            collection.ReplaceAll(valueAsString.Where(v => !string.IsNullOrWhiteSpace(v)));
        }

    }

    public static class HashSetExtensions
    {
        public static void ReplaceAll(this HashSet<string> collection, IEnumerable<string> newItems)
        {
            collection.Clear();

            if (newItems == null) return;

            foreach (var item in newItems)
            {
                collection.Add(item);
            }
        }
    }

}