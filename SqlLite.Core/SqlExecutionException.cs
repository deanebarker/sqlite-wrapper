using System;
using System.Collections.Generic;
using System.Text;

namespace DeaneBarker.SqlLite
{
    // This class exists solely to catch the SQL and parameters of an error just to make it easier to debug
    public class SqlExecutionException : Exception
    {
        public string Sql { get; set; }
        public Dictionary<string, object> Parameters { get; set; }

        public SqlExecutionException(string message, Exception innerException, string sql, object parameters) : base(message, innerException)
        {
            Sql = sql;
            Parameters = Database.ConvertParameterObject(parameters);
        }
    }
}
