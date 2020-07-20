using System;
using System.Collections.Generic;
using System.Text;

namespace DeaneBarker.SqlLite.Columns
{
    public abstract class BaseColumn
    {
        public string Name { get; protected set; }

        protected string columnName;
        public string ColumnName => columnName ?? Name;
        protected string DataType { get; set; }
        protected abstract string CalcRawSqlValue(object value);


        public Type OutputType { get; protected set; }
        public List<string> Constraints { get; protected set; }

        public BaseColumn(string name, params string[] constraints)
        {
            Name = name;
            Constraints = new List<string>();
            Constraints.AddRange(constraints);
        }

        public virtual string GetDdl()
        {
            return $"{Name} {DataType} {string.Join(' ', Constraints)}".Trim();
        }

        public string GetRawSqlValue(object value)
        {
            if (value == null) return Table.NULL_VALUE;

            return CalcRawSqlValue(value);
        }
    }
}
