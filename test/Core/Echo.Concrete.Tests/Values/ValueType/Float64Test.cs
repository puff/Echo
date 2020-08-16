using System;
using Echo.Concrete.Values.ValueType;
using Xunit;

namespace Echo.Concrete.Tests.Values.ValueType
{
    public class Float64Test
    {
        [Fact]
        public void PersistentBits()
        {
            const double testValue = 0.123D;
            
            var value = new Float64Value(testValue);

            Span<byte> buffer = stackalloc byte[sizeof(double)];
            Span<byte> mask = stackalloc byte[sizeof(double)];
            value.GetBits(buffer);
            value.GetMask(mask);
            
            value.SetBits(buffer, mask);
            
            Assert.Equal(testValue, value.F64);
        }

        [Theory]
        [InlineData(1.5D, 1.25D, 2.75D)]
        [InlineData(100.5D, 1.25D, 101.75D)]
        public void AddFullyKnownValues(double a, double b, double expected)
        {
            var value1 = new Float64Value(a);
            var value2 = new Float64Value(b);
            value1.Add(value2);
            Assert.Equal(expected, value1.F64);
        }
    }
}