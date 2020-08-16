using System;
using Echo.Concrete.Values.ValueType;
using Xunit;

namespace Echo.Concrete.Tests.Values.ValueType
{
    public class Float32Test
    {
        [Fact]
        public void IsZeroTest()
        {
            var value = new Float32Value(0);
            Assert.Equal(true, value.IsZero);
        }
        
        [Fact]
        public void IsPositiveInfinity()
        {
            var value = new Float32Value(float.PositiveInfinity);
            Assert.Equal(true, value.IsInfinity);
            Assert.Equal(true, value.IsPositiveInfinity);
            Assert.Equal(false, value.IsNegativeInfinity);
        }
        
        [Fact]
        public void IsNegativeInfinity()
        {
            var value = new Float32Value(float.NegativeInfinity);
            Assert.Equal(true, value.IsInfinity);
            Assert.Equal(false, value.IsPositiveInfinity);
            Assert.Equal(true, value.IsNegativeInfinity);
        }
        
        [Fact]
        public void IsNaN()
        {
            var value = new Float32Value(float.NaN);
            Assert.Equal(true, value.IsNaN);
        }
        
        [Fact]
        public void PersistentBits()
        {
            const float testValue = 0.123f;
            
            var value = new Float32Value(testValue);
            
            Span<byte> buffer = stackalloc byte[sizeof(float)];
            Span<byte> mask = stackalloc byte[sizeof(float)];
            value.GetBits(buffer);
            value.GetMask(mask);
            
            value.SetBits(buffer, mask);
            
            Assert.Equal(testValue, value.F32);
        }

        [Theory]
        [InlineData(1.5f, 1.25f, 2.75f)]
        [InlineData(100.5f, 1.25f, 101.75f)]
        public void AddFullyKnownValues(float a, float b, float expected)
        {
            var value1 = new Float32Value(a);
            var value2 = new Float32Value(b);
            value1.Add(value2);
            Assert.Equal(expected, value1.F32);
        }
    }
}