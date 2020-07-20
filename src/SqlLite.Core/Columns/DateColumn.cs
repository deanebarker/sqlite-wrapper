using System;
using System.Collections.Generic;
using System.Text;

namespace DeaneBarker.SqlLite.Columns
{
    public class DateColumn : BaseColumn
    {
        public DateColumn(string name, params string[] constraints) : base(name, constraints)
        {
            OutputType = typeof(DateTime);
            DataType = "TEXT";
        }

        protected override string CalcRawSqlValue(object value)
        {
            try
            {
                var convertedValue = (DateTime)Convert.ChangeType(value, typeof(DateTime));
                return $"datetime('{convertedValue:yyyy-MM-ddThh:mm:ss}')";
            }
            catch (Exception e)
            {
                if (e is FormatException || e is InvalidCastException || e is OverflowException)
                {
                    throw new FormatException($"Error converting value for column \"{Name}\". Value: \"{(value.ToString().Length > 50 ? value.ToString().Substring(0, 50) + "..." : value.ToString())}\"", e);
                }
                throw;
            }
        }
    }
}
