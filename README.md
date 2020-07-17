# Deane's SQLite Wrapper

To be clear: _this is not an object-relational mapper (ORM)_. Those tools already exist for SQLite, and this is not one of them.

This is simply a wrapper around SQLite to automate some operations. This library assumes you're fine with SQL in general, and it never attempts to completely hide SQL or its uses. It just makes simple things even simpler.

For example:

(This code assumes you've installed the `System.Data.SQLite` Nuget package.

```csharp
// Create a new database, or open an existing database
// To create it in memory instead, just don't pass anything into the constructor
var db = new Database(@"C:\friends.db");  

// Add a table
// The table and all its columns will be created automatically, if they don't already exist
// Every table will get a auto-incrementing NumberColumn named "id" automatically, unless 
db.AddTable(new Table("friends",
    new TextColumn("first_name", "NOT NULL"),
    new TextColumn("last_name", "NOT NULL"),
    new NumberColumn("age")
));

// One way to add data
// You need to pass in a value for every column you created above, in the same order
db["friends"].AddRecordValues("Ross", "Gellar", 30);

// Another way to add data
// Property names map to column names
db["friends"].AddRecord(new { first_name = "Monica", last_name = "Gellar", age = 28});

// Yet another way to add data
// Dictionary keys map to column names
db["friends"].AddRecord(new Dictionary<string, object>() {
    ["first_name"] = "Chandler",
    ["last_name"] = "Bing",
    ["age"] = 30
});

// Get a datareader
var myFriendsDataReader = db.Query("SELECT * FROM friends");

// Get a strongly-typed dictionary of type <string, object> (the name fields will be strings, the age field will be a long)
// Types are derived from the columns types used above to populate the table, NOT the actual underlying tables
// Remember that every table gets an "id" column automatically, and this will be returned as a long
var myFriendsDictionary = db.TypedQuery("friends");

var desiredLastName = "Gellar"
var mySiblingDictionary = db.TypedQuery("friends", "last_name = @last_name", "first_name ASC", new { last_name = desiredLastName });

// Get a scalar value
var numberOfOldFriends = db.GetValue<int>("SELECT count(*) FROM friends WHERE age = 30");
);

// Execute arbitrary SQL
// Property names of the object should map to parameter keys
db.Execute("UPDATE friends SET last_name = @last_name WHERE first_name = @first_name", new { 
    last_name = "the Divorce Force",
    first_name = "Ross"
});
```

A key to understanding this library is that the object representation of a database only needs to refer to the tables and columns that you want to work with.

You might open and work with an existing SQLite database created by some other process. If you only work with a single column in a single table, just do this:

```csharp
var db = new Database(@"C:\existing-database.db");
db.AddTable(new Table("an-existing-table", new TextColumn("an-existing-column")));
db["an-existing-table"].AddRecordValues("my value");
```

The one table and column you want to affect is all that this library needs to know about. Since the table and column already exist, they won't be changed. There could be hundreds of other tables in the database (or hundreds of other columns in the table), and this code does not care.

Additionally, some other code somewhere else might create a `Database` object based on that same database _file_ and work with an entirely different set of tables of columns. Each `Database` object would be completely ignorant of what the other one was doing or what structures it's working with.

(One way of looking at this is that the `Database` object isn't an actual database. Rather, it's a _communication protocol_ with a database. So, it's one method of communicating with a specific database for a specific purpose.)

If you're working with an existing database, and you _know_ all your tables and columns already exist, you can flag the `Database` object to not check for them by setting the `TrustBackingDatabase` flag, which would make the code faster in situations where you create the `Database` object often.

```csharp
var db = new Database("existing-database.db");
db.TrustBackingDatabase = true;
db.AddTable(new Table("an-existing-table", new TextColumn("an-existing-column")));
```

That will save you one database query for each table, and another for each column.

Obviously that value has to be set be _before_ you add a table. Also, if that table doesn't actually exist, Very Bad Things will start to happen when you try to refer to it (that's the "trust" part...).