using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace dclmgd.Support
{
    struct BitArray64 : IEnumerable<bool>
    {
        ulong value;
        readonly int length;

        public BitArray64(int length) => (this.length, value) = (length, 0);

        public bool this[int idx]
        {
            get => (value & (1UL << idx)) != 0;
            set
            {
                if (value)
                    this.value |= 1UL << idx;
                else
                    this.value &= ~(1UL << idx);
            }
        }

        public IEnumerator<bool> GetEnumerator()
        {
            for (int bit = 0; bit < length; ++bit)
                yield return this[bit];
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
