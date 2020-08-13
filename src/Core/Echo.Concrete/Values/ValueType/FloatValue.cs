using System;
using System.Text;
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
        public virtual bool IsKnown
        {
            get
            {
                Span<byte> mask = stackalloc byte[Size];
                GetMask(mask);
                
                // Verify mask is all 1s.
                for (int i = 0; i < Size; i++)
                {
                    if (mask[i] != 0xFF)
                        return false;
                }
                
                return true;
            }
        }

        /// <inheritdoc />
        public abstract int Size
        {
            get;
        }

        /// <inheritdoc />
        public bool IsValueType => true;

        /// <inheritdoc />
        public virtual Trilean IsZero
        {
            get
            {
                Span<byte> bits = stackalloc byte[Size];
                GetBits(bits);

                if (IsKnown)
                {
                    for (var i = 0; i < Size; i++)
                    {
                        if (bits[i] != 0)
                            return Trilean.False;
                    }

                    return Trilean.True;
                }

                Span<byte> mask = stackalloc byte[Size];
                GetMask(mask);
                
                var bitField = new BitField(bits);
                var maskField = new BitField(mask);
                
                bitField.And(maskField);
                
                for (int i = 0; i < bits.Length * 8; i++)
                {
                    if (bitField[i])
                        return Trilean.False;
                }

                return Trilean.Unknown;
            }
        }

        /// <inheritdoc />
        public virtual Trilean IsNonZero => !IsZero;

        /// <inheritdoc />
        public Trilean IsPositive => !Sign;

        /// <inheritdoc />
        public Trilean IsNegative => Sign;

        /// <summary>
        /// Gets a value indicating the floating point number is a result of an invalid operation.
        /// </summary>
        public virtual Trilean IsNaN
        {
            get
            {
                Span<byte> bits = stackalloc byte[Size];
                Span<byte> mask = stackalloc byte[Size];
                GetBits(bits);
                GetMask(mask);

                var bitField = new BitField(bits);
                var maskField = new BitField(mask);

                // Verify at least one bit in significand is 1.
                Trilean atLeastOneSet = false;
                for (int i = 0; i < SignificandSize && !atLeastOneSet; i++)
                {
                    if (!maskField[i + SignificantIndex])
                        atLeastOneSet |= Trilean.Unknown;
                    else if (bitField[i + SignificantIndex])
                        atLeastOneSet = true;
                }

                // Verify exponent is all 1s.
                for (int i = 0; i < ExponentSize; i++)
                {
                    if (!bitField[i + ExponentIndex])
                        return Trilean.False;
                }

                return atLeastOneSet;
            }
        }

        /// <summary>
        /// Gets a value indicating whether the floating point number is equal to either positive or negative infinity. 
        /// </summary>
        public Trilean IsInfinity
        {
            get
            {
                if (!IsKnown)
                    return Trilean.Unknown;
                
                Span<byte> bits = stackalloc byte[Size];
                GetBits(bits);

                var bitField = new BitField(bits);

                // Verify significand is all 0s.
                for (int i = 0; i < SignificandSize; i++)
                {
                    if (bitField[i + SignificantIndex])
                        return Trilean.False;
                }
                
                // Verify exponent is all 1s.
                for (int i = 0; i < ExponentSize; i++)
                {
                    if (!bitField[i + ExponentIndex])
                        return Trilean.False;
                }
                
                return Trilean.True;
            }
        }

        /// <summary>
        /// Gets a value indicating whether the floating point number is equal to positive infinity. 
        /// </summary>
        public virtual Trilean IsPositiveInfinity => !Sign & IsInfinity;

        /// <summary>
        /// Gets a value indicating whether the floating point number is equal to negative infinity. 
        /// </summary>
        public virtual Trilean IsNegativeInfinity => Sign & IsInfinity;

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
        
        private int ExponentIndex => SignificantIndex + SignificandSize;

        private int SignificantIndex => 0;

        private int SignIndex => ExponentIndex + ExponentSize;

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

        /// <inheritdoc />
        public override string ToString()
        {
            Span<byte> bits = stackalloc byte[Size];
            Span<byte> mask = stackalloc byte[Size];
            GetBits(bits);
            GetMask(mask);

            var bitField = new BitField(bits);
            var maskField = new BitField(mask);

            var builder = new StringBuilder();

            builder.Append(Sign.Value switch
            {
                TrileanValue.False => "+",
                TrileanValue.True => "-",
                TrileanValue.Unknown => "(-1)^? * ",
                _ => throw new ArgumentOutOfRangeException()
            });

            builder.Append(IsZero ? "0." : "1.");

            for (int i = SignificantIndex + SignificandSize - 1; i >= SignificantIndex; i--)
                WriteBit(builder, bitField[i], maskField[i]);
            
            builder.Append("₂ * 2^");
            for (int i = ExponentIndex + ExponentSize - 1; i >= ExponentIndex; i--)
                WriteBit(builder, bitField[i], maskField[i]);

            static void WriteBit(StringBuilder builder, bool value, bool mask)
            {
                if (!mask)
                    builder.Append('?');
                else if (value)
                    builder.Append('1');
                else
                    builder.Append('0');
            }

            builder.Append('₂');

            return builder.ToString();
        }
    }
}