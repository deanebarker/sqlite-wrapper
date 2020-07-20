using DeaneBarker.SqlLite.Columns;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;

namespace DeaneBarker.SqlLite
{
    public class Table
    {
        public const string EXPLICIT_ID_COLUMN_NAME = "id";
        public const string IMPLICIT_ID_COLUMN_NAME = "ROWID";
        public const string NULL_VALUE = "null";

        public readonly List<BaseColumn> columns;
        public ReadOnlyCollection<BaseColumn> Columns => columns.AsReadOnly();
        public ReadOnlyCollection<BaseColumn> UserColumns => columns.Where(c => c.Name != IdColumnName).ToList().AsReadOnly();
        public string Name { get; set; }

        // If set to true, this table will not create an explicit ID and will instead use the default ROWID
        // It's handy for existing tables that use the implicit style
        public bool UseImplicitId { get; set; }

        public string IdColumnName => HasColumn(EXPLICIT_ID_COLUMN_NAME) ? EXPLICIT_ID_COLUMN_NAME : IMPLICIT_ID_COLUMN_NAME;

        // This is set right before the table is added in Database. The table will use this to access the commands
        // to actually run SQL (Execure, Query, etc.)
        internal Database Database;

        // You can create a table by name and columns, at the same time.
        public Table(string name, params BaseColumn[] specifiedColumns)
        {
            Name = name;
            columns = new List<BaseColumn>();

            if (specifiedColumns != null)
            {
                columns.AddRange(specifiedColumns);
            }
        }

        // Column indexer. Using this and the indexer on database, you can address columns by db["tableName"]["columnName"] syntax
        public BaseColumn this[string name]
        {
            get
            {
                if (!HasColumn(name)) return null; // We want to give them the ability to check for a null here without throwing the exception in GetColumn
                return GetColumn(name);
            }
        }

        // Adds a column. Not commonly used since columns can be added in the constructor.
        public void AddColumn(BaseColumn column)
        {
            if (Database != null)
            {
                throw new Exception("Unable to add column. Table already created.");
            }
            columns.Add(column);
        }

        public void CreateIdColumn()
        {
            // This is called by the database before it creates the table to ensure we have an ID field
            // In SQLite, this will effectively alias the ROWID column
            columns.Insert(0, new IdColumn());
        }

        // Looks up a column by name
        public BaseColumn GetColumn(string name)
        {
            var column = Columns.FirstOrDefault(t => t.Name.Equals(name, StringComparison.CurrentCultureIgnoreCase));
            if (column == null) throw new Exception($"Column not found: {name}"); // We're throwing the exception here because any attempt to get a column that doesn't exist is likely a problem that would likely manifest later in weird ways if we just returned a null
            return column;
        }

        public bool HasColumn(string name)
        {
            return Columns.Any(t => t.Name.Equals(name, StringComparison.CurrentCultureIgnoreCase));
        }

        // Deletes a single record by ID
        public void DeleteRecord(long id)
        {
            var sql = $"DELETE FROM {Name} WHERE {IdColumnName} = @{IdColumnName}";
            Database.Execute(sql, new Dictionary<string, object>() { [IdColumnName] = id });
        }

        // Gets a single record by ID
        public Dictionary<string, object> GetRecord(long id)
        {
            var reader = Database.Query($"SELECT * FROM {Name} WHERE {IdColumnName} = @{IdColumnName}", new Dictionary<string, object>() { { IdColumnName, id } });
            if (!reader.HasRows)
            {
                return null;
            }

            return new TypedRecordset(this, reader).Records.First();
        }

        // Gets the total number of records
        public long GetRecordCount()
        {
            return Database.GetValue<long>($"SELECT COUNT(*) FROM {Name}");
        }

        // Updates a single field of a single record
        public void UpdateValue(string columnName, object value, long id)
        {
            var record = new Record()
            {
                [columnName] = value
            };

            UpdateRecord(record, id);
        }

        // Adds a record
        public long AddRecord(object recordObject)
        {
            var record = Database.ConvertParameterObject(recordObject);

            // Make sure we have every column
            foreach (var columnName in record.Keys)
            {
                var column = GetColumn(columnName);
                if (column == null)
                {
                    throw new Exception($"Column not found: {columnName}");
                }
            }

            var sb = new StringBuilder();
            sb.AppendLine($"INSERT INTO {Name} ");
            sb.AppendLine("(");
            sb.AppendJoin(", " + Environment.NewLine, record.Select(r => r.Key));
            sb.AppendLine(")");
            sb.AppendLine("VALUES");
            sb.AppendLine("(");
            sb.AppendJoin("," + Environment.NewLine, record.Select(r => GetColumn(r.Key).GetRawSqlValue(r.Value)));
            sb.AppendLine(")");

            Database.Execute(sb.ToString());
            return GetLastId();
        }

        // Convenience method for adding a record without having to specify column names
        public long AddRecordValues(params object[] values)
        {
            if (UserColumns.Count != values.Count())
            {
                throw new Exception($"Column count mismatch. Table \"{Name}\" requires {UserColumns.Count()} column(s).");
            }

            // Create the Record
            var record = new Record();
            for (int x = 0; x < values.Count(); x++)
            {
                record[UserColumns[x].Name] = values[x];
            }

            return AddRecord(record);
        }

        // Adds a record
        public void UpdateRecord(object recordObject, long id)
        {
            UpdateRecord(recordObject, $"{IdColumnName} = @{IdColumnName}", new Dictionary<string, object>() { [IdColumnName] = id });
        }

        public void UpdateRecord(object recordObject, string whereClause, object parameters)
        {
            var record = Database.ConvertParameterObject(recordObject);

            // Make sure we have every column
            foreach (var columnName in record.Keys)
            {
                var column = GetColumn(columnName);
                if (column == null)
                {
                    throw new Exception($"Column not found: {columnName}");
                }
            }

            var sb = new StringBuilder();
            sb.AppendLine($"UPDATE {Name} SET ");
            sb.AppendJoin(", " + Environment.NewLine, record.Select(r => string.Concat(r.Key, " = ", GetColumn(r.Key).GetRawSqlValue(r.Value))));
            sb.Append(" WHERE ");
            sb.AppendLine(whereClause);

            Database.Execute(sb.ToString(), Database.ConvertParameterObject(parameters));
        }

        // Gets the last ID inserted into this table
        public long GetLastId()
        {
            return Database.GetValue<long>($"SELECT seq FROM sqlite_sequence WHERE name = '{Name}'");
        }

        public string GetDdl()
        {
            var sb = new StringBuilder();
            sb.AppendLine($"CREATE TABLE {Name}");
            sb.AppendLine("(");
            sb.AppendLine("id INTEGER PRIMARY KEY AUTOINCREMENT,"); /// This column is an unbreakable convention
            sb.AppendJoin(", ", columns.Where(c => c.Name != IdColumnName).Select(c => c.GetDdl() + Environment.NewLine));
            sb.AppendLine(")");

            return sb.ToString();
        }
    }
}
