using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Linq;
using System.Text;
using System.Threading;

namespace DeaneBarker.SqlLite
{
    public class TypedRecordset
    {
        private readonly Table table;
        private readonly SQLiteDataReader reader;

        public TypedRecordset(Table table, SQLiteDataReader reader)
        {
            this.table = table;
            this.reader = reader;
        }

        public IEnumerable<Dictionary<string, object>> Records
        {
            get
            {
                if (!reader.HasRows)
                {
                    yield break;
                }

                while (reader.Read())
                {
                    yield return GetDictionaryFromDataReader(reader);
                }
            }
        }

        private Dictionary<string, object> GetDictionaryFromDataReader(SQLiteDataReader reader)
        {
            var results = new Dictionary<string, object>();
            if (table.UseImplicitId)
            {
                results[Table.IMPLICIT_ID_COLUMN_NAME] = (long)reader[0];
            }

            foreach (var column in table.Columns)
            {
                results[column.Name] = Convert.ChangeType(reader[column.Name], column.OutputType);
            }
            return results;
        }
    }
}
