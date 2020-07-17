using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data.SQLite;
using System.Linq;

namespace DeaneBarker.SqlLite
{
    public class Database : IDisposable
    {
        private const string MEMORY_CONNECTION_STRING = ":memory:";
        private const string SQL_EXCEPTION_MESSAGE = "Error during SQL execution";

      

        private readonly List<Table> tables;
        public ReadOnlyCollection<Table> Tables => tables.AsReadOnly();

        private readonly SQLiteConnection conn;

        // If set, this delegate executes immediately prior to a command formation. Use it for debugging SQL, but be careful -- it will
        // execute A LOT. Don't do anything dumb in here.
        public Action<string, Dictionary<string, object>> LogSql = null;

        // If set to true, this object will "trust" that its backing database has the necessary tables and columns. Use this when you KNOW
        // the schema of the database has not changed, or that you have set it up manually. It will make the object faster, especially when
        // created often, since it will skip the table/column checks. However, if the database doesn't have the required tables/columns, your
        // code WILL throw errors...
        // Also: obviously, this needs to be set to true BEFORE you add tables.
        public bool TrustBackingDatabase { get; set; }

        public Database(string path = MEMORY_CONNECTION_STRING)
        {
            try
            {
                conn = new SQLiteConnection($"Data Source={path}");
                conn.Open();
            }
            catch(Exception e)
            {
                throw new Exception("Unable to create database", e);
            }

            tables = new List<Table>();
        }

        // Unless you're trusting the database, the tables and columns are created or altered in the backing database here.
        public void AddTable(Table table)  
        {
            // Every table has to have a "id" column, unless UseImplicitId is set (in which case it will use the default ROWID column)
            if (!table.UseImplicitId && !table.HasColumn(Table.EXPLICIT_ID_COLUMN_NAME))
            {
                table.CreateIdColumn();
            }

            // The table needs a reference back to the "mothership"
            table.Database = this;

            tables.Add(table);

            if (!TrustBackingDatabase)
            {
                // This is where we make sure the database behind this object model can actually support the configured model
                // Specifically, we're going to see if there's anything we need to ADD to this database to support this table
                // As a rule, we NEVER DELETE anything from the database. There could be all kinds of extra stuff in there, and we don't care.
                // We only need to know that are tables and columns to support this specific object's stuff. In fact, two different Database objects
                // might have entirely different object models, but use the SAME database, each ignorant of the tables and columns the other one uses.

                // We're gonna save the necessary commands up and execute them as a transaction
                var toExecute = new List<string>();

                // Do we have a backing table for this table object?
                var tableRecords = Query($"SELECT name FROM sqlite_master WHERE type = 'table' AND name = @name", new Dictionary<string, object>() { ["name"] = table.Name });
                if (!tableRecords.HasRows)
                {
                    // No table. Create it, with all its configured columns
                    toExecute.Add(table.GetDdl());
                }
                else
                {
                    // We do have the table, so let's check that we have all the columns
                    table.Columns.ToList().ForEach(c =>
                    {
                        var columnRecords = Query($"SELECT * FROM PRAGMA_TABLE_INFO('{table.Name}') WHERE name = @name", new Dictionary<string, object>() { ["name"] = c.Name });
                        if (!columnRecords.HasRows)
                        {
                        // This column doesn't exist, create it
                        toExecute.Add($"ALTER TABLE {table.Name} ADD COLUMN {c.GetDdl()}");
                        }
                    });
                }

                // Batch execute all the database changes
                if (toExecute.Any())
                {
                    BeginTransaction();
                    toExecute.ForEach(s => Execute(s));
                    CommitTransaction();
                }
            }
        }

        public Table GetTable(string name)
        {
            var table = Tables.FirstOrDefault(t => t.Name.Equals(name, StringComparison.CurrentCultureIgnoreCase));
            if (table == null) throw new Exception($"Table not found: {name}"); // We're throwing the exception here because any attempt to get a table that doesn't exist is likely a problem that would likely manifest later in weird ways if we just returned a null
            return table;
        }

        public bool HasTable(string name)
        {
            return Tables.Any(t => t.Name.Equals(name, StringComparison.CurrentCultureIgnoreCase));
        }

        // The indexer. It allows referencing of tables by db["table_name"] syntax
        public Table this[string name]
        {
            get
            {
                if (!HasTable(name)) return null; // We want to give them the ability to check for a null here without throwing the exception in GetTable
                return GetTable(name);
            }
        }

        public TypedRecordset TypedQuery(string tableName, string where = null, string sort = null, Dictionary<string, object> parameters = null)
        {
            // We have to control the SQL here, because we need to make sure that ALL columns are returned.
            // TODO: is this necessary, though? Could we make the TypedQuery "SQL SELECT-centric," rather than "object-centric"?

            sort ??= string.Concat(Table.EXPLICIT_ID_COLUMN_NAME);
            where ??= "true";

            var table = GetTable(tableName);
            var columnNames = table.UseImplicitId ? "ROWID, *" : "*";

            var sql = $"SELECT {columnNames} FROM {table.Name} WHERE {where} ORDER BY {sort}";
            return new TypedRecordset(GetTable(tableName), Query(sql, parameters));
        }

        // I'm keeping this public in the name of flexibility. There might be some situations where getting a raw command object would be helpful.
        public SQLiteCommand GetCommand(string sql, Dictionary<string, object> parameters = null)
        {
            // If we're logging, do it
            LogSql?.Invoke(sql, parameters);

            var cmd = new SQLiteCommand(sql, conn);

            // Translate the parameters
            if (parameters != null)
            {
                foreach (var parameter in parameters)
                {
                    cmd.Parameters.AddWithValue(parameter.Key, parameter.Value);
                }
            }

            return cmd;
        }

        public void BeginTransaction(string mode = "DEFERRED")
        {
            Execute($"BEGIN {mode}");
        }

        public void CommitTransaction()
        {
            Execute("COMMIT");
        }


        /* 
            These methods are LITERALLY the only places where SQL is actually executed.
            We trap errors in a SqlExecutionException so we can store the attempted SQL for debugging.
        */


        // Execute queries that should return data
        public SQLiteDataReader Query(string sql, object parameters = null)
        {
            var cmd = GetCommand(sql, ConvertParameterObject(parameters));

            try
            {
                return cmd.ExecuteReader();
            }
            catch(Exception e)
            {
                throw new SqlExecutionException(SQL_EXCEPTION_MESSAGE, e, sql, parameters);
            }
        }

        // Execute non-queries
        public void Execute(string sql, object parameters = null)
        {
            var cmd = GetCommand(sql, ConvertParameterObject(parameters));      

            try
            {
                cmd.ExecuteNonQuery();
            }
            catch(Exception e)
            {
                throw new SqlExecutionException(SQL_EXCEPTION_MESSAGE, e, sql, parameters);
            }
        }

        // Gets a single typed value
        public T GetValue<T>(string sql, object parameters = null)
        {
            var cmd = GetCommand(sql, ConvertParameterObject(parameters));

            object value;
            try
            {
                value = cmd.ExecuteScalar();
            }
            catch(Exception e)
            {
                throw new SqlExecutionException(SQL_EXCEPTION_MESSAGE, e, sql, parameters);
            }

            if(value == null)
            {
                return default;  // Return the default of whatever T is
            }

            // We have to use "Convert" because the int/long conversion gets weird sometimes.
            // If you call with <int>, it gets annoyed trying to cast to a long.
            return (T)Convert.ChangeType(value, typeof(T));
        }

        public static Dictionary<string, object> ConvertParameterObject(object parameters)
        {
            if(parameters == null)
            {
                return new Dictionary<string, object>();
            }

            if (parameters is Dictionary<string, object>)
            {
                return (Dictionary<string, object>)parameters;
            }

            var result = new Dictionary<string, object>();
            foreach (var prop in parameters.GetType().GetProperties())
            {
                result.Add(prop.Name, prop.GetValue(parameters));
            }
            return result;
        }

        public void Dispose()
        {
            conn.Close();
        }
    }
}
