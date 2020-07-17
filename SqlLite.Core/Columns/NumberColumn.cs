using System;
using System.Collections.Generic;
using System.Text;

namespace DeaneBarker.SqlLite.Columns
{
    public class NumberColumn : BaseColumn
    {
        public NumberColumn(string name, params string[] constraints) : base(name, constraints)
        {
            OutputType = typeof(long);
            DataType = "INT";
        }

        protected override string CalcRawSqlValue(object value)
        {
            try
            {
                var convertedValue = Convert.ChangeType(value, typeof(long));
                return convertedValue.ToString();
            }
            catch (Exception e)
            {
                if (e is FormatException || e is InvalidCastException || e is OverflowException)
                {
                    throw new FormatException($"Error converting value for column {Name}. Value: {value}", e);
                }
                throw;
            }
        }
    }
}
