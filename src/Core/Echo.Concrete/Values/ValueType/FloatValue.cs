using System;
using Echo.Core;
using Echo.Core.Values;

namespace Echo.Concrete.Values.ValueType
{
    /// <summary>
    /// Represents a primitive floating point value that might contain unknown bits.
    /// </summary>
    public abstract class FloatValue : IValueTypeValue
    {
        /// <inheritdoc />
        public abstract bool IsKnown
        {
            get;
        }

        /// <inheritdoc />
        public abstract int Size
        {
            get;
        }

        /// <inheritdoc />
        public bool IsValueType => true;

        /// <inheritdoc />
        public abstract Trilean IsZero
        {
            get;
        }

        /// <inheritdoc />
        public abstract Trilean IsNonZero
        {
            get;
        }

        /// <inheritdoc />
        public Trilean IsPositive => !Sign;

        /// <inheritdoc />
        public Trilean IsNegative => Sign;

        /// <summary>
        /// Gets a value indicating the floating point number is a result of an invalid operation.
        /// </summary>
        public abstract Trilean IsNaN
        {
            get;
        }

        /// <summary>
        /// Gets a value indicating whether the floating point number is equal to either positive or negative infinity. 
        /// </summary>
        public Trilean IsInfinity => IsPositiveInfinity | IsNegativeInfinity;

        /// <summary>
        /// Gets a value indicating whether the floating point number is equal to positive infinity. 
        /// </summary>
        public abstract Trilean IsPositiveInfinity
        {
            get;
        }

        /// <summary>
        /// Gets a value indicating whether the floating point number is equal to negative infinity. 
        /// </summary>
        public abstract Trilean IsNegativeInfinity
        {
            get;
        }

        /// <summary>
        /// Gets or sets the sign bit of the number.
        /// </summary>
        public abstract Trilean Sign
        {
            get;
            set;
        }

        /// <summary>
        /// Gets the number of bits that make up the exponent.
        /// </summary>
        public abstract int ExponentSize
        {
            get;
        }

        /// <summary>
        /// Gets the number of bits that make up the significand.
        /// </summary>
        public abstract int SignificandSize
        {
            get;
        }

        /// <summary>
        /// Gets a value indicating the exponent bias that is used in the representation of the bias.
        /// </summary>
        public int ExponentBias => (1 << ExponentSize) - 1;

        /// <inheritdoc />
        public abstract void GetBits(Span<byte> buffer);

        /// <inheritdoc />
        public abstract void GetMask(Span<byte> buffer);

        /// <inheritdoc />
        public abstract void SetBits(Span<byte> bits, Span<byte> mask);

        /// <inheritdoc />
        public abstract IValue Copy();
    }
}