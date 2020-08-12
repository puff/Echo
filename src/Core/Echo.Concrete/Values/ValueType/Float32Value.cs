using System;
using System.Buffers.Binary;
using System.Globalization;
using Echo.Core;
using Echo.Core.Values;

namespace Echo.Concrete.Values.ValueType
{
    /// <summary>
    /// Represents a fully known concrete 32 bit floating point numerical value.
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
        /// Creates a new fully known concrete 32 bit floating point numerical value.
        /// </summary>
        /// <param name="value">The raw 32 bit value.</param>
        public Float32Value(float value)
        {
            F32 = value;
        }

        /// <summary>
        /// Gets or sets the raw floating point value.
        /// </summary>
        public float F32
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
        public override void GetMask(Span<byte> buffer)
        {
            // TODO: support unknown bits in float.
            buffer.Fill(0xFF);
        }

        /// <inheritdoc />
        public override unsafe void SetBits(Span<byte> bits, Span<byte> mask)
        {
            // HACK: .NET standard 2.0 does not provide a method to read floating point numbers from a span.
            // TODO: support unknown bits in float.
            
            uint rawBits = BinaryPrimitives.ReadUInt32LittleEndian(bits);
            F32 = *(float*) &rawBits;
        }

        /// <inheritdoc />
        public override IValue Copy() => new Float32Value(F32);

        /// <inheritdoc />
        public override string ToString() => F32.ToString(CultureInfo.InvariantCulture);

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
        public override int GetHashCode() => F32.GetHashCode();
    }
}