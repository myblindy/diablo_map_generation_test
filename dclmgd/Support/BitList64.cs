using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace dclmgd.Support
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Linq;
    using System.Runtime.CompilerServices;

    /// <summary>
    /// A collection of bits (exposed as <see cref="bool"/> values) that can either be 1 (<c>True</c>) or
    /// 0 (<c>False</c>).
    /// </summary>
    /// <remarks>
    /// This is a replacement for <see cref="BitArray"/> with more advanced functionality. Bitmaps
    /// are useful when performing unions/intersections on large datasets.
    /// 
    /// Data is encoded in <see cref="ulong"/> blocks where the right-most bit in each block
    /// represents the 0th-place.
    /// </remarks>
    public class BitList64 : IEnumerable<bool>
    {
        /// <summary>
        /// The number of bits in a <see cref="ulong"/>.
        /// </summary>
        private const int BitsInULong = sizeof(ulong) * 8;

        /// <summary>
        /// The collection of <see cref="ulong"/>s representing the bits in the bitmap.
        /// </summary>
        private readonly ulong[] bits;

        /// <summary>
        /// The size of the bitmap collection (in bits).
        /// </summary>
        /// <remarks>
        /// This is likely to be smaller than the number of bits that exist within
        /// <see cref="bits"/> as we must round up when constructing the <see cref="ulong"/> array
        /// in order to fully store the requested bit size.
        /// </remarks>
        public int Length { get; }

        /// <summary>
        /// Get or set the value of a bit at the specified index.
        /// </summary>
        /// <param name="index">The index of the bit to get or set.</param>
        /// <returns>True if the bit is set (1); otherwise, false.</returns>
        public bool this[int index]
        {
            get => GetBit(index);
            set => SetBit(index, value);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Bitmap"/> class with the specified length
        /// setting all bits to the (optionally) specified initial value.
        /// </summary>
        /// <param name="length">The length, in bits, of the bitmap.</param>
        /// <param name="initialValue">The initial value to set each bit to.</param>
        public BitList64(int length, bool initialValue = false)
        {
            Length = length;
            bits = new ulong[length / BitsInULong + 1];

            if (initialValue)
                SetAllBits(initialValue);
        }

        /// <summary>
        /// Sets all the bits in the bitmap to the specified value.
        /// </summary>
        /// <param name="value">The value to set each bit to.</param>
        /// <returns>The instance of the modified bitmap.</returns>
        public BitList64 SetAllBits(bool value)
        {
            for (int index = 0; index < bits.Length; index++)
                bits[index] = value ? ulong.MaxValue : ulong.MinValue;

            return this;
        }

        /// <summary>
        /// Gets the value of the bit at the specified index.
        /// </summary>
        /// <param name="index">The index of the bit.</param>
        /// <returns>The value of the bit at the specified index.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool GetBit(int index)
        {
            if (!IsValidIndex(index))
                throw new ArgumentOutOfRangeException(nameof(index));

            return (bits[index / BitsInULong] & (0x1ul << (index % BitsInULong))) > 0;
        }

        /// <summary>
        /// Sets the value of the bit at the specified index to the specified value.
        /// </summary>
        /// <param name="index">The index of the bit to set.</param>
        /// <param name="value">The value to set the bit to.</param>
        /// <returns>The instance of the modified bitmap.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public BitList64 SetBit(int index, bool value)
        {
            if (!IsValidIndex(index))
                throw new ArgumentOutOfRangeException(nameof(index));

            if (value)
                bits[index / BitsInULong] |= 0x1ul << (index % BitsInULong);
            else
                bits[index / BitsInULong] &= ~(0x1ul << (index % BitsInULong));

            return this;
        }

        /// <summary>
        /// Takes the union of this bitmap and the specified bitmap and stores the result in this
        /// instance.
        /// </summary>
        /// <param name="bitmap">The bitmap to union with this instance.</param>
        /// <returns>A reference to this instance.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public BitList64 Union(BitList64 bitmap)
        {
            if (Length != bitmap.Length)
                throw new ArgumentException("Bitmaps must be of equal length to union them.", nameof(bitmap));

            for (int index = 0; index < bits.Length; index++)
                bits[index] |= bitmap.bits[index];

            return this;
        }

        /// <summary>
        /// Takes the intersection of this bitmap and the specified bitmap and stores the result in
        /// this instance.
        /// </summary>
        /// <param name="bitmap">The bitmap to interesct with this instance.</param>
        /// <returns>A reference to this instance.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public BitList64 Intersect(BitList64 bitmap)
        {
            if (Length != bitmap.Length)
                throw new ArgumentException("Bitmaps must be of equal length to intersect them.", nameof(bitmap));

            for (int index = 0; index < bits.Length; index++)
                bits[index] &= bitmap.bits[index];

            return this;
        }

        /// <summary>
        /// Inverts all the bits in this bitmap.
        /// </summary>
        /// <returns>A reference to this instance.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public BitList64 Invert()
        {
            for (int index = 0; index < bits.Length; index++)
                bits[index] = ~bits[index];

            return this;
        }

        /// <summary>
        /// Sets a range of bits to the specified value.
        /// </summary>
        /// <param name="start">The index of the bit at the start of the range (inclusive).</param>
        /// <param name="end">The index of the bit at the end of the range (inclusive).</param>
        /// <param name="value">The value to set the bits to.</param>
        /// <returns>A reference to this instance.</returns>
        public BitList64 SetRange(int start, int end, bool value)
        {
            if (!IsValidIndex(start))
                throw new ArgumentOutOfRangeException(nameof(start));
            if (!IsValidIndex(end))
                throw new ArgumentOutOfRangeException(nameof(end));
            if (start > end)
                throw new ArgumentException("Range is inverted.", nameof(end));
            if (start == end)
                return SetBit(start, value);

            int startBucket = start / BitsInULong;
            int startOffset = start % BitsInULong;
            int endBucket = end / BitsInULong;
            int endOffset = end % BitsInULong;

            if (value)
                bits[startBucket] |= ulong.MaxValue << startOffset;
            else
                bits[startBucket] &= ~(ulong.MaxValue << startOffset);

            for (int bucketIndex = startBucket + 1; bucketIndex < endBucket; bucketIndex++)
                bits[bucketIndex] = value ? ulong.MaxValue : ulong.MinValue;

            if (value)
                bits[endBucket] |= ulong.MaxValue >> (BitsInULong - endOffset - 1);
            else
                bits[endBucket] &= ~(ulong.MaxValue >> (BitsInULong - endOffset - 1));

            return this;
        }

        /// <summary>
        /// Gets the individual set of bits that are enabled in this bitmap.
        /// </summary>
        /// <returns>
        /// An <see cref="ISet"/> object containing the index of all enabled bits.
        /// </returns>
        public ISet<int> GetTrueBits()
        {
            var trueBits = new HashSet<int>();

            for (int bucketIndex = 0; bucketIndex < bits.Length; bucketIndex++)
            {
                var bucket = bits[bucketIndex];
                int bitIndex = 0;
                while (bucket > 0 && bucketIndex * BitsInULong + bitIndex < Length)
                {
                    if ((bucket & 0x1) > 0)
                        trueBits.Add(bucketIndex * BitsInULong + bitIndex);

                    bucket >>= 1;
                    bitIndex++;
                }
            }

            return trueBits;
        }

        /// <inheritdoc/>
        public IEnumerator<bool> GetEnumerator()
        {
            for (int index = 0; index < Length; index++)
                yield return GetBit(index);
        }

        /// <inheritdoc/>
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        /// <inheritdoc/>
        public override bool Equals(object obj) => obj is BitList64 bitmap && bits.SequenceEqual(bitmap.bits);

        /// <inheritdoc/>
        public override int GetHashCode()
        {
            var hashCode = 671604886;
            hashCode = bits.Aggregate(hashCode, (agg, next) => agg * -1521134295 + next.GetHashCode());
            hashCode = hashCode * -1521134295 + Length.GetHashCode();
            return hashCode;
        }

        /// <summary>
        /// Checks whether or not the specified index is valid (within bounds) for this bitmap.
        /// </summary>
        /// <param name="index">The index to check.</param>
        /// <returns>True if valid; otherwise, false.</returns>
        private bool IsValidIndex(int index) => index >= 0 && index < Length;

        /// <summary>
        /// Checks and returns if all bits are set to <c>true</c>.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool AllTrue() =>
            bits.SkipLast(1).All(w => w == ulong.MaxValue) && (bits.Last() & (1UL << (Length % BitsInULong))) - 1 == (ulong.MaxValue & (1UL << (Length % BitsInULong)));
    }
}
