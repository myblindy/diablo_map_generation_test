using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace dclmgd.Support
{
    class EnumArray<TKey, TValue> where TKey : Enum, IEnumerable<TValue>, IList<TValue>
    {
    }
}
