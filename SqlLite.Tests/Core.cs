using DeaneBarker.SqlLite;
using DeaneBarker.SqlLite.Columns;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NuGet.Frameworks;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.IO;
using System.Linq;
using System.Net.Http.Headers;

namespace SqlLite.Tests
{
    [TestClass]
    public class Core
    {

        [TestMethod]
        public void Operations()
        {
            var db = new Database();
            var cars = new Table("cars");
            cars.AddColumn(new TextColumn("name"));  
            db.AddTable(cars); // Columns have to be added before the table is added, because when the table is added, the DDL is executed

            Assert.AreEqual(1, db.Tables.Count());
            Assert.AreEqual("name", db.Tables.First().Columns.Skip(1).First().Name);
        }

        [TestMethod]
        public void ManualQueries()
        {
            var db = GetDb();

            // Check data query
            var result1 = db.GetValue<string>("SELECT name FROM cars");
            Assert.AreEqual("Porsche", result1);

            // Check auto-increment key
            var result2 = db.GetValue<long>("SELECT id FROM cars WHERE name = 'Audi'");
            Assert.AreEqual(2, result2);
        }

        [TestMethod]
        public void AddRecord()
        {
            var db = new Database();
            var cars = new Table("cars");
            cars.AddColumn(new TextColumn("name"));
            db.AddTable(cars);

            var record = new Record
            {
                ["name"] = "BMW"
            };
            db["cars"].AddRecord(record);

            var result = db.GetValue<string>("SELECT name FROM cars ORDER BY id DESC");
            Assert.AreEqual("BMW", result);
        }


        [TestMethod]
        public void SimplifiedRecordAdding()
        {
            var db = GetDb();

            var result = db.GetValue<string>("SELECT name FROM cars");
            Assert.AreEqual("Porsche", result);
        }

        [TestMethod]
        public void UpdateValue()
        {
            var db = GetDb();

            db["cars"].UpdateValue("price", 10000L, 1);

            var result = db.GetValue<long>("SELECT price FROM cars");
            Assert.AreEqual(10000L, result);
        }

        [TestMethod]
        public void UpdateRecord()
        {
            var db = GetDb();
            db["cars"].UpdateRecord(new Record() { ["name"] = "BMW" }, 1);
                
            Assert.AreEqual("BMW", db["cars"].GetRecord(1)["name"]);

            db["cars"].UpdateRecord(new { name = "Chevy" }, "name = @name", new { name = "BMW" });

            Assert.AreEqual("Chevy", db["cars"].GetRecord(1)["name"]);

        }

        [TestMethod]
        public void RecordCountAndDeleteRecord()
        {
            var db = GetDb();

            Assert.AreEqual(2, db["cars"].GetRecordCount());

            db["cars"].DeleteRecord(2);

            Assert.AreEqual(1, db["cars"].GetRecordCount());
        }

        [TestMethod]
        public void TableIndexer()
        {
            var db = GetDb();

            Assert.AreEqual(2, db["cars"].GetRecordCount());
        }

        [TestMethod]
        public void GetRecord()
        {
            var db = GetDb();

            Assert.AreEqual("Porsche", db["cars"].GetRecord(1)["name"]);
            Assert.IsNull(db["cars"].GetRecord(3));
        }

        [TestMethod]
        public void CreateFileDbAndDispose()
        {
            var fileName = Guid.NewGuid() + ".db";
            var filePath = string.Concat(@"C:\Windows\Temp\", fileName);

            using (var db = new Database(filePath))
            {
                Assert.IsTrue(File.Exists(filePath));
            }

            File.Delete(filePath);
        }
        
        [TestMethod]
        public void InsertWithApostrophe()
        {
            using var db = GetDb();
            db["cars"].AddRecordValues("Foster's", 1000L);
            // No test. It just needs to run without throw an exception
        }

        [TestMethod]
        public void MissingTableInIndexer()
        {
            using var db = GetDb();
            if(db["blah"] == null)
            {
                // No test; it should just not throw an exception
            }
        }

        [TestMethod]
        public void ReflectDatabase()
        {
            // NOTE: I don't think this test proves anything anymore...
            // We don't "reflect" a database anymore...

            var fileName = Guid.NewGuid() + ".db";
            var filePath = string.Concat(@"C:\Windows\Temp\", fileName);

            using (var db1 = new Database(filePath))
            {
                db1.AddTable(new Table("people", new TextColumn("name")));
            }
            // database should be cleared here

            using var db2 = new Database(filePath);
            db2.AddTable(new Table("people", new TextColumn("name"), new NumberColumn("age")));
            db2.AddTable(new Table("cars", new TextColumn("name")));

            Assert.IsTrue(db2.HasTable("people"));
            Assert.AreEqual(2, db2["people"].UserColumns.Count);
            Assert.IsTrue(db2.HasTable("cars"));
        }

        [TestMethod]
        public void HasTable()
        {
            var db = GetDb();

            Assert.IsTrue(db.HasTable("cars"));
            Assert.IsFalse(db.HasTable("people"));
        }

        [TestMethod]
        [ExpectedException(typeof(FormatException), "Invalid data was inserted.")]
        public void TypeConversionFunctionsAndTypedDataReader()
        {
            var db = new Database();
            db.AddTable(new Table("posts", new TextColumn("title"), new DateColumn("created"), new NumberColumn("comments")));

            db["posts"].AddRecordValues("My Post Title", new DateTime(1971, 9, 3), 100L);
            db["posts"].AddRecordValues("My Other Post Title", new DateTime(1971, 9, 3), 100L);

            // Get an empty reader
            var emptyReader = db.TypedQuery("posts", "id > 10000");
            Assert.AreEqual(0, emptyReader.Records.Count());

            // Ensure the WHERE clause works
            Assert.AreEqual(1, db.TypedQuery("posts", "id < 2").Records.Count());

            // Ensure the SORT works
            Assert.AreEqual("My Other Post Title", db.TypedQuery("posts", null, "id DESC").Records.First()["title"]);

            // This reader will return data
            var rows = db.TypedQuery("posts").Records.ToList();
            var row1 = rows[0];
            var row2 = rows[1];

            Assert.AreEqual(1L, row1["id"]);
            Assert.AreEqual(2L, row2["id"]);

            // Check the resulting types
            Assert.IsTrue(row1["id"] is long);
            Assert.IsTrue(row1["title"] is string);
            Assert.IsTrue(row1["created"] is DateTime);
            Assert.IsTrue(row1["comments"] is long);

            // This should throw an expected exception
            db["posts"].AddRecordValues("This should break", "blah", new DateTime(1971,9,3));
        }

        [TestMethod]
        public void DateTimeSelect()
        {
            var db = new Database();
            db.AddTable(new Table("people", new TextColumn("title"), new DateColumn("dob")));

            db["people"].AddRecordValues("Deane", new DateTime(1971, 9, 3));
            db["people"].AddRecordValues("Gabrielle", new DateTime(2001, 9, 17));

            Assert.AreEqual(1, db.GetValue<long>("SELECT COUNT(*) FROM people WHERE datetime(dob) > datetime('2000-01-01')"));
        }

        [TestMethod]
        public void DateInsertion()
        {
            var db = new Database();
            db.AddTable(new Table("posts", new TextColumn("title"), new DateColumn("created")));

            db["posts"].AddRecordValues("My Post Title", new DateTime(1971, 9, 3, 7, 0, 0));
            var row = db["posts"].GetRecord(1);

            Assert.AreEqual(row["created"], new DateTime(1971, 9, 3, 7, 0, 0 ));
        }

        [TestMethod]
        public void EnsureNulls()
        {
            var db = GetDb();
            db["cars"].AddRecordValues(null, 0L);

            Assert.AreEqual(1, db.GetValue<long>("SELECT COUNT(*) FROM cars WHERE name IS NULL"));
        }

        [TestMethod]
        [ExpectedException(typeof(SqlExecutionException), "Constained data was allowed to be inserted")]
        public void ColumnWithConstraints()
        {
            var db = new Database();
            db.AddTable(new Table("people", new TextColumn("name", "UNIQUE")));

            db["people"].AddRecordValues("Deane");
            db["people"].AddRecordValues("Deane"); // should fail the unique constraint
        }

        [TestMethod]
        public void UseImplicitId()
        {
            var table = new Table("friends", new TextColumn("first_name")) { UseImplicitId = true };
            var db = new Database();
            db.AddTable(table);

            // We shouldn't have added the "id" column
            Assert.IsFalse(db["friends"].HasColumn("id"));     

            //We should still be able to add and delete by ID
            db["friends"].AddRecordValues("Chandler Bing");
            Assert.AreEqual(1L, db["friends"].GetRecordCount());
            Assert.AreEqual(1L, db.TypedQuery("friends", null, null, null).Records.First()["ROWID"]);
            db["friends"].DeleteRecord(1);
            Assert.AreEqual(0, db["friends"].GetRecordCount());
        }


        private Database GetDb()
        {
            var db = new Database();
            db.AddTable(new Table("cars",
                new TextColumn("name"),
                new NumberColumn("price")
            ));

            db["cars"].AddRecordValues("Porsche", 100000L);
            db["cars"].AddRecordValues("Audi", 200000L);
            return db;
        }
    }
}
