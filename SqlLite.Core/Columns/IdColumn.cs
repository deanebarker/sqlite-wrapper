using System;
using System.Collections.Generic;
using System.Text;

namespace DeaneBarker.SqlLite.Columns
{
    public class IdColumn : NumberColumn
    {
        public IdColumn() : base(Table.EXPLICIT_ID_COLUMN_NAME)
        {
            DataType = "INTEGER";
            Constraints.Add("PRIMARY KEY");
            Constraints.Add("AUTOINCREMENT");
        }
    }
}
