using System;
using System.Buffers.Binary;
using Echo.Core;
using Echo.Core.Values;

namespace Echo.Concrete.Values.ValueType
{
    /// <summary>
    /// Represents a (partially) known concrete 32 bit floating point numerical value.
    /// </summary>
    public class Float32Value : FloatValue
    {
        /// <summary>
        /// Wraps a 32 bit floating point number into an instance of <see cref="Float32Value"/>.
        /// </summary>
        /// <param name="value">The 32 bit floating point number to wrap.</param>
        /// <returns>The concrete 32 bit floating point numerical value.</returns>
        public static implicit operator Float32Value(float value)
        {
            return new Float32Value(value);
        }
        
        /// <summary>
        /// Represents the bitmask that is used for a fully known concrete 32 bit floating point value. 
        /// </summary>
        public const uint FullyKnownMask = 0xFFFFFFFF;

        private const uint ExponentMask = 0b0_11111111_00000000000000000000000;
        private const uint SignificandMask = 0b0_00000000_11111111111111111111111;
        private const uint DefaultSignificandMask = 0b1_11111111_00000000000000000000000;
        private const int SignIndex = sizeof(float) * 8 - 1;
        private static readonly Integer8Value ExponentBiasValue = new Integer8Value(127);

        /// <summary>
        /// Creates a new fully known concrete 32 bit floating point numerical value.
        /// </summary>
        /// <param name="value">The raw 32 bit value.</param>
        public Float32Value(float value)
        {
            F32 = value;
            Mask = FullyKnownMask;
        }

        /// <summary>
        /// Creates a new partially known concrete 32 bit floating point numerical value.
        /// </summary>
        /// <param name="value">The raw 32 bit value.</param>
        /// <param name="mask">The known bit mask.</param>
        public Float32Value(float value, uint mask)
        {
            F32 = value;
            Mask = mask;
        }

        /// <summary>
        /// Gets or sets the raw floating point value.
        /// </summary>
        public float F32
        {
            get;
            set;
        }

        /// <summary>
        /// Gets a value indicating which bits in the floating point number are known.
        /// If bit at location <c>i</c> equals 1, bit <c>i</c> in <see cref="F32"/> and <see cref="F32"/> is known,
        /// and unknown otherwise.  
        /// </summary>
        public uint Mask
        {
            get;
            set;
        }

        /// <inheritdoc />
        public override int Size => sizeof(float);

        /// <inheritdoc />
        public override Trilean Sign
        {
            get => F32 < 0;
            set => throw new NotImplementedException();
        }

        /// <inheritdoc />
        public override int ExponentSize => 8;

        /// <inheritdoc />
        public override int SignificandSize => 23;

        /// <inheritdoc />
        public override unsafe void GetBits(Span<byte> buffer)
        {
            // HACK: .NET standard 2.0 does not provide a method to write floating point numbers to a span.
            
            float value = F32;
            uint rawBits = *(uint*) &value;
            BinaryPrimitives.WriteUInt32LittleEndian(buffer, rawBits);
        }

        /// <inheritdoc />
        public override void GetMask(Span<byte> buffer) => BinaryPrimitives.WriteUInt32LittleEndian(buffer, Mask);

        /// <inheritdoc />
        public override unsafe void SetBits(Span<byte> bits, Span<byte> mask)
        {
            // HACK: .NET standard 2.0 does not provide a method to read floating point numbers from a span.
            
            uint rawBits = BinaryPrimitives.ReadUInt32LittleEndian(bits);
            F32 = *(float*) &rawBits;
            Mask = BinaryPrimitives.ReadUInt32LittleEndian(mask);
        }

        /// <inheritdoc />
        public override void MarkFullyUnknown()
        {
            F32 = 0;
            Mask = 0;
        }

        /// <inheritdoc />
        protected override unsafe IntegerValue GetExponent()
        {
            if (IsZero)
                return new Integer8Value(0);
            
            float value = F32;
            uint rawBits = (*(uint*) &value & ExponentMask) >> SignificandSize;
            uint maskBits = (Mask & ExponentMask) >> SignificandSize;
            
            var result = new Integer8Value((byte) rawBits, (byte) maskBits);
            result.Subtract(ExponentBiasValue);
            return result;
        }

        /// <inheritdoc />
        protected override unsafe IntegerValue GetSignificand()
        {
            if (IsZero)
                return new Integer8Value(0);
            
            float value = F32;
            uint rawBits = *(uint*) &value & SignificandMask;
            uint maskBits = (Mask & SignificandMask) | DefaultSignificandMask;

            if (IsZero.Value != TrileanValue.True)
                rawBits |= 1u << SignificandSize;

            return new Integer32Value(rawBits, maskBits);
        }

        /// <inheritdoc />
        protected override IntegerValue GetExponentBias() => ExponentBiasValue;

        /// <inheritdoc />
        public override IValue Copy() => new Float32Value(F32);

        /// <inheritdoc />
        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj))
                return false;
            if (ReferenceEquals(this, obj))
                return true;
            
            return obj is Float32Value value && F32.Equals(value.F32);;
        }

        /// <inheritdoc />
        public override void Add(FloatValue other)
        {
            if (IsKnown && other.IsKnown && other is Float32Value float32)
                F32 += float32.F32;
            else
                base.Add(other);
        }

        /// <inheritdoc />
        public override int GetHashCode() => F32.GetHashCode();
    }
}