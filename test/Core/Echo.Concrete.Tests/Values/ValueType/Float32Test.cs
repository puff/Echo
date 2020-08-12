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
    }
}