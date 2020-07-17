using System;
using System.Collections.Generic;
using System.Text;

namespace DeaneBarker.SqlLite.Columns
{
    public class TextColumn : BaseColumn
    {
        public TextColumn(string name, params string[] constraints) : base(name, constraints)
        {
            OutputType = typeof(string);
            DataType = "TEXT";
        }

        protected override string CalcRawSqlValue(object value)
        {
            var convertedValue = (string)Convert.ChangeType(value, typeof(string));
            convertedValue = convertedValue.Replace("'", "''");
            return $"'{convertedValue}'";
        }
    }
}
