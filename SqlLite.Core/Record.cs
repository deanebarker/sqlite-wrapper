using System;
using System.Collections.Generic;
using System.Text;

namespace DeaneBarker.SqlLite
{
    // Abstracting this in case we want to add stuff later
    // Also "Record" is so much easier to type than "Dictionary<string, object>"...
    public class Record : Dictionary<string, object>
    {
    }
}
