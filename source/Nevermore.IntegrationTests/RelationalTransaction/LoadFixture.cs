﻿using System.Linq;
using FluentAssertions;
using Nevermore.IntegrationTests.Model;
using Nevermore.IntegrationTests.SetUp;
using NUnit.Framework;
#pragma warning disable 618

namespace Nevermore.IntegrationTests.RelationalTransaction
{
    public class LoadFixture : FixtureWithRelationalStore
    {
        [Test]
        public void LoadWithSingleId()
        {
            using (var trn = Store.BeginTransaction())
            {
                trn.Load<Product>("A");
            }
        }

        [Test]
        public void LoadWithMultipleIds()
        {
            using (var trn = Store.BeginTransaction())
            {
                trn.Load<Product>(new[] { "A", "B" });
            }
        }

        [Test]
        public void LoadWithMoreThan2100Ids()
        {
            using (var trn = Store.BeginTransaction())
            {
                trn.Load<Product>(Enumerable.Range(1, 3000).Select(n => "ID-" + n));
            }
        }

        [Test]
        public void LoadStreamWithMoreThan2100Ids()
        {
            using (var trn = Store.BeginTransaction())
            {
                trn.LoadStream<Product>(Enumerable.Range(1, 3000).Select(n => "ID-" + n));
            }
        }

        [Test]
        public void StoreNonInheritedTypesSerializesCorrectly()
        {
            using var trn = Store.BeginTransaction();
            
            var customer = new Customer
            {
                FirstName = "Bob",
                LastName = "Tester",
                Nickname = "Bob the builder",
                Id = "Customers-01"
            };

            trn.Insert(customer);

            var customers = trn.Stream<(string Id, string FirstName, string Nickname, string JSON)>("select Id, FirstName, Nickname, [JSON] from TestSchema.Customer").ToList();

            var c = customers.Single(p => p.Id == "Customers-01");
            c.FirstName.Should().Be("Bob", "Type isn't serializing into column correctly");
            c.Nickname.Should().Be("Bob the builder", "Type isn't serializing into column correctly");
            c.JSON.Should().Be("{\"LuckyNumbers\":null,\"ApiKey\":null,\"Passphrases\":null}");
        }

        [Test]
        public void StoreEnumInheritedTypesSerializesCorrectlyForNormalProduct()
        {
            using (var trn = Store.BeginTransaction())
            {
                var originalNormal = new Product()
                {
                    Name = "Unicorn Dust",
                    Id = "UD-01",
                    Price = 11.1m,
                };

                trn.Insert(originalNormal);

                var allProducts = trn.Stream<(string Id, string Type, string JSON)>("select Id, Type, [JSON] from TestSchema.Product").ToList();

                var special = allProducts.Single(p => p.Id == "UD-01");
                special.Type.Should().Be("Normal", "Type isn't serializing into column correctly");
                special.JSON.Should().Be("{\"Price\":11.1}");
            }
        }

        [Test]
        public void StoreEnumInheritedTypesSerializesCorrectlyForSpecialProduct()
        {
            using (var trn = Store.BeginTransaction())
            {
                var originalSpecial = new SpecialProduct
                {
                    Name = "Unicorn Dust",
                    BonusMaterial = "Directors Commentary",
                    Id = "UD-01",
                    Price = 11.1m,
                };

                var originalDud = new DodgyProduct
                {
                    Id = "DO-01",
                    Name = "Something",
                    Price = 12.3m,
                    Tax = 15m
                };

                trn.Insert(originalSpecial);
                trn.Insert(originalDud);

                var allProducts = trn.Stream<(string Id, string Type, string JSON)>("select Id, Type, [JSON] from TestSchema.Product").ToList();

                var special = allProducts.Single(p => p.Id == "UD-01");
                special.Type.Should().Be("Special", "Type isn't serializing into column correctly");
                special.JSON.Should().Be("{\"BonusMaterial\":\"Directors Commentary\",\"Price\":11.1}");
            }
        }

        [Test]
        public void StoreAndLoadEnumInheritedTypes()
        {
            using (var trn = Store.BeginTransaction())
            {
                var originalNormal = new Product
                {
                    Name = "Norm",
                    Id = "NL-01",
                    Price = 15.5m
                };

                var originalSpecial = new SpecialProduct
                {
                    Name = "Unicorn Dust",
                    BonusMaterial = "Directors Commentary",
                    Id = "UD-01",
                    Price = 11.1m
                };

                var originalDud = new DodgyProduct
                {
                    Id = "DO-01",
                    Name = "Something",
                    Price = 12.3m,
                    Tax = 15m
                };

                trn.Insert(originalNormal);
                trn.Insert<SpecialProduct>(originalSpecial);
                trn.Insert<DodgyProduct>(originalDud);

                var allProducts = trn.Query<Product>().ToList();
                Assert.True(allProducts.Exists(p =>
                    p is SpecialProduct sp && sp.BonusMaterial == originalSpecial.BonusMaterial), "Special product didn't load correctly");
                Assert.True(allProducts.Exists(p => p is DodgyProduct dp && dp.Tax == originalDud.Tax), "Dodgy product didn't load correctly");

                var onlySpecial = trn.Query<SpecialProduct>().ToList();
                onlySpecial.Count.Should().Be(1);
                onlySpecial[0].BonusMaterial.Should().Be(originalSpecial.BonusMaterial);
            }
        }

        [Test]
        public void StoreStringInheritedTypesSerializeCorrectly()
        {
            using (var trn = Store.BeginTransaction())
            {
                var brandA = new BrandA { Name = "Brand A", Description = "Details for Brand A." };
                var brandB = new BrandB { Name = "Brand B", Description = "Details for Brand B." };

                trn.Insert<Brand>(brandA);
                trn.Insert<Brand>(brandB);
                trn.Commit();

                var allBrands = trn.Stream<(string Name, string JSON)>("select Name, [JSON] from TestSchema.Brand").ToList();

                allBrands.SingleOrDefault(x => x.Name == "Brand A").Should().NotBeNull("Didn't retrieve BrandA");
                var brandToTestSerialization = allBrands.Single(x => x.Name == "Brand A");
                brandToTestSerialization.JSON.Should().Be("{\"Description\":\"Details for Brand A.\"}");
            }
        }
        
        [Test]
        public void StoreAndLoadStringInheritedTypes()
        {
            using (var trn = Store.BeginTransaction())
            {
                var brandA = new BrandA { Name = "Brand A", Description = "Details for Brand A." };
                var brandB = new BrandB { Name = "Brand B", Description = "Details for Brand B." };

                trn.Insert<Brand>(brandA);
                trn.Insert<Brand>(brandB);
                trn.Commit();

                var allBrands = trn.Query<Brand>().ToList();

                allBrands.SingleOrDefault(x => x.Name == "Brand A").Should().NotBeNull("Didn't retrieve BrandA");
                allBrands.Single(x => x.Name == "Brand A").Should().BeOfType<BrandA>();
            }
        }

        [Test]
        public void StoreAndLoadStringInheritedTypesThatAreProperties()
        {
            using (var trn = Store.BeginTransaction())
            {
                var machineA = new Machine { Name = "Machine A", Description = "Details for Machine A.", Endpoint = new PassiveTentacleEndpoint { Name = "Quiet tentacle" }};
                var machineB = new Machine { Name = "Machine B", Description = "Details for Machine B.", Endpoint = new ActiveTentacleEndpoint { Name = "Noisy tentacle" } };

                trn.Insert(machineA);
                trn.Insert(machineB);
                trn.Commit();

                var allMachines = trn.Query<Machine>().ToList();

                allMachines.SingleOrDefault(x => x.Name == "Machine A").Should().NotBeNull("Didn't retrieve Machine A");
                allMachines.Single(x => x.Name == "Machine A").Endpoint.Should().BeOfType<PassiveTentacleEndpoint>();
            }
        }

        [Test]
        public void StoreAndLoadFromParameterizedRawSql()
        {
            using (var trn = Store.BeginTransaction())
            {
                InsertProductAndLineItems("Unicorn Hair", 2m, trn, 1);         // subtotal: $2 of Unicorn Hair
                InsertProductAndLineItems("Unicorn Poop", 3m, trn, 2, 3);      // subtotal: $15 of Unicorn Poop
                InsertProductAndLineItems("Unicorn Dust", 1m, trn, 2, 1, 7);   // subtotal: $10 of Unicorn Dust
                InsertProductAndLineItems("Fairy Bread", 10m, trn, 4);         // subtotal: $40 of Fairy Bread
                trn.Commit();

                var productSubtotalQuery =    @"SELECT
                                                    Id,
                                                    ProductId,
                                                    ProductName,
                                                    Subtotal
                                                FROM (
                                                    SELECT 
                                                        CAST(NEWID() AS nvarchar(36)) Id,
                                                        p.ProductId,
                                                        p.ProductName,
                                                        SUM(p.Price * l.Quantity) Subtotal
                                                    FROM (
                                                        SELECT
                                                            ProductId,
                                                            CAST(JSON_VALUE([JSON], '$.Quantity') AS int) Quantity
                                                        FROM TestSchema.LineItem
                                                    ) l
                                                    INNER JOIN (
                                                    SELECT 
                                                        Id ProductId,
                                                        [Name] ProductName,
                                                        CAST(JSON_VALUE([JSON], '$.Price') AS decimal) Price
                                                        FROM TestSchema.Product
                                                    ) p ON p.ProductId = l.ProductId
                                                    GROUP BY p.ProductId, p.ProductName
                                                ) p3
                                                WHERE Subtotal >= @MinimumSubtotal";

                var doubleDigitUnicornProductSubtotals = trn.RawSqlQuery<ProductSubtotal>(productSubtotalQuery)
                    .Where(s => s.ProductName.Contains("Unicorn"))
                    .Parameter("MinimumSubtotal", 10)
                    .ToList();

                doubleDigitUnicornProductSubtotals.Should().HaveCount(2);
                doubleDigitUnicornProductSubtotals.Should().ContainSingle(s => s.ProductName == "Unicorn Poop").Which.Subtotal.Should().Be(15m);
                doubleDigitUnicornProductSubtotals.Should().ContainSingle(s => s.ProductName == "Unicorn Dust").Which.Subtotal.Should().Be(10m);
            }
        }

        void InsertProductAndLineItems(string productName, decimal productPrice, IWriteTransaction trn, params int[] quantities)
        {
            var product = new Product
            {
                Name = productName,
                Price = productPrice
            };
            trn.Insert(product);
            foreach (var quantity in quantities)
            {
                var lineItem = new LineItem { ProductId = product.Id, Name = "Some line item", Quantity = quantity };
                trn.Insert(lineItem);
            }
        }
    }
}