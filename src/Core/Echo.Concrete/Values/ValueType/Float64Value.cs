using System;
using System.Buffers.Binary;
using System.Globalization;
using Echo.Core;
using Echo.Core.Values;

namespace Echo.Concrete.Values.ValueType
{
    /// <summary>
    /// Represents a fully known concrete 64 bit floating point numerical value.
    /// </summary>
    public class Float64Value : FloatValue
    {
        /// <summary>
        /// Wraps a 64 bit floating point number into an instance of <see cref="Float64Value"/>.
        /// </summary>
        /// <param name="value">The 64 bit floating point number to wrap.</param>
        /// <returns>The concrete 64 bit floating point numerical value.</returns>
        public static implicit operator Float64Value(double value)
        {
            return new Float64Value(value);
        }

        /// <summary>
        /// Represents the bitmask that is used for a fully known concrete 64 bit floating point value. 
        /// </summary>
        public const ulong FullyKnownMask = 0xFFFFFFFF_FFFFFFFF;

        private const ulong ExponentMask = 0b0_11111111111_0000000000000000000000000000000000000000000000000000;
        private const ulong SignificandMask = 0b0_00000000000_1111111111111111111111111111111111111111111111111111;
        private const ulong DefaultSignificandMask = 0b1_11111111111_0000000000000000000000000000000000000000000000000000;
        private static readonly Integer16Value ExponentBiasValue = new Integer16Value(1023);

        /// <summary>
        /// Creates a new fully known concrete 64 bit floating point numerical value.
        /// </summary>
        /// <param name="value">The raw 64 bit value.</param>
        public Float64Value(double value)
        {
            F64 = value;
            Mask = FullyKnownMask;
        }

        /// <summary>
        /// Creates a new partially known concrete 64 bit floating point numerical value.
        /// </summary>
        /// <param name="value">The raw 64 bit value.</param>
        /// <param name="mask">The known bit mask.</param>
        public Float64Value(double value, ulong mask)
        {
            F64 = value;
            Mask = mask;
        }

        /// <summary>
        /// Gets or sets the raw floating point value.
        /// </summary>
        public double F64
        {
            get;
            set;
        }
        
        /// <summary>
        /// Gets a value indicating which bits in the floating point number are known.
        /// If bit at location <c>i</c> equals 1, bit <c>i</c> in <see cref="F64"/> and <see cref="F64"/> is known,
        /// and unknown otherwise.  
        /// </summary>
        public ulong Mask
        {
            get;
            set;
        }

        /// <inheritdoc />
        public override int Size => sizeof(double);

        /// <inheritdoc />
        public override Trilean Sign
        {
            get => F64 < 0;
            set => throw new NotImplementedException();
        }

        /// <inheritdoc />
        public override int ExponentSize => 11;

        /// <inheritdoc />
        public override int SignificandSize => 52;

        /// <inheritdoc />
        public override void MarkFullyUnknown()
        {
            F64 = 0;
            Mask = 0;
        }

        /// <inheritdoc />
        public override IValue Copy() => new Float64Value(F64);

        /// <inheritdoc />
        protected override unsafe IntegerValue GetExponent()
        {
            if (IsZero)
                return new Integer16Value(0);
            
            double value = F64;
            ulong rawBits = (*(ulong*) &value & ExponentMask) >> SignificandSize;
            ulong maskBits = ((Mask & ExponentMask) >> SignificandSize) | 0b11111000_00000000;
            
            var result = new Integer16Value((ushort) rawBits, (ushort) maskBits);
            result.Subtract(ExponentBiasValue);
            return result;
        }

        /// <inheritdoc />
        protected override unsafe IntegerValue GetSignificand()
        {
            if (IsZero)
                return new Integer64Value(0);
            
            double value = F64;
            ulong rawBits = *(ulong*) &value & SignificandMask;
            ulong maskBits = (Mask & SignificandMask) | DefaultSignificandMask;

            if (IsZero.Value != TrileanValue.True)
                rawBits |= 1ul << SignificandSize;

            return new Integer64Value(rawBits, maskBits);
        }

        /// <inheritdoc />
        protected override IntegerValue GetExponentBias() => ExponentBiasValue;

        /// <inheritdoc />
        public override unsafe void GetBits(Span<byte> buffer)
        {
            // HACK: .NET standard 2.0 does not provide a method to write floating point numbers to a span.
            
            double value = F64;
            ulong rawBits = *(ulong*) &value;
            BinaryPrimitives.WriteUInt64LittleEndian(buffer, rawBits);
        }

        /// <inheritdoc />
        public override void GetMask(Span<byte> buffer) => BinaryPrimitives.WriteUInt64LittleEndian(buffer, Mask);

        /// <inheritdoc />
        public override unsafe void SetBits(Span<byte> bits, Span<byte> mask)
        {
            // HACK: .NET standard 2.0 does not provide a method to read floating point numbers from a span.
            // TODO: support unknown bits in float.
            
            ulong rawBits = BinaryPrimitives.ReadUInt64LittleEndian(bits);
            F64 = *(double*) &rawBits;
        }

        /// <inheritdoc />
        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj))
                return false;
            if (ReferenceEquals(this, obj))
                return true;
            
            return obj is Float64Value value && F64.Equals(value.F64);;
        }

        /// <inheritdoc />
        public override int GetHashCode() => F64.GetHashCode();

        /// <inheritdoc />
        public override void Add(FloatValue other)
        {
            if (IsKnown && other.IsKnown && other is Float64Value float64)
                F64 += float64.F64;
            else
                base.Add(other);
        }
        
    }
}