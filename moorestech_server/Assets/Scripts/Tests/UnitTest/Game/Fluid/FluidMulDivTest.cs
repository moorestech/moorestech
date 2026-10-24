using System;
using System.Numerics;
using Game.Fluid.Simulation;
using NUnit.Framework;

namespace Tests.UnitTest.Game.Fluid
{
    /// <summary>
    ///     FluidMulDiv.MulDiv が 128 ビットの途中値で a × b / c を正確に求める(0 へ向けて切り捨て)ことを、BigInteger の厳密値と突き合わせる。
    ///     FluidMulDiv.MulDiv computes a × b / c exactly through a 128-bit intermediate (truncating toward zero), checked against exact BigInteger values.
    /// </summary>
    public class FluidMulDivTest
    {
        [Test]
        // 積が 64 ビットを超える境界値、符号の組み合わせ、タンク × 容量比と比例縮小の実際の形
        // Boundary values whose product exceeds 64 bits, sign combinations, and the real shapes: tank × capacity ratio and proportional scaling
        public void 境界値と符号の組み合わせが厳密値と一致するTest()
        {
            AssertExact(long.MaxValue, long.MaxValue, long.MaxValue);
            AssertExact(-long.MaxValue, long.MaxValue, long.MaxValue);
            AssertExact(1L << 62, 1L << 62, 1L << 62);
            AssertExact(long.MaxValue, 1, long.MaxValue);
            AssertExact(-5, 3, 2);
            AssertExact(5, -3, 2);
            AssertExact(-5, -3, 2);
            AssertExact(0, 12345, 7);
            AssertExact(1, 1, 1);
            AssertExact(50_000_000_000_000, 500_000_000_000, 50_000_000_000_000);
            AssertExact(30_000_000_000, 50_000_000_000_000, 50_000_000_001);
            AssertExact(-30_000_000_000, 50_000_000_000_000, 50_000_000_001);
        }

        [Test]
        // 固定シードの乱数で、商が long に収まる形(|b| ≤ c)を多数照合する
        // Many fixed-seed random cases in the shape whose quotient fits in a long (|b| ≤ c)
        public void 乱数の多数例が厳密値と一致するTest()
        {
            var random = new Random(20261024);
            for (var i = 0; i < 200_000; i++)
            {
                var a = RandomLong(random, random.Next(1, 64));
                var c = Math.Max(1, (long)RandomBits(random, random.Next(1, 64)));
                var b = (long)RandomBits(random, random.Next(1, 64));
                if (c < b) b %= c + 1;
                if (random.Next(2) == 0) b = -b;
                AssertExact(a, b, c);
            }
        }

        [Test]
        public void 除数が0以下なら例外Test()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => FluidMulDiv.MulDiv(1, 1, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => FluidMulDiv.MulDiv(1, 1, -3));
        }

        [Test]
        // 64 ビットに収まる積と収まらない積のどちらの経路でも、商が long を超えれば例外
        // Both paths, product within 64 bits and beyond, throw when the quotient exceeds a long
        public void 商がlongに収まらなければ例外Test()
        {
            Assert.Throws<OverflowException>(() => FluidMulDiv.MulDiv(long.MaxValue, 2, 1));
            Assert.Throws<OverflowException>(() => FluidMulDiv.MulDiv(long.MaxValue, long.MaxValue, 1));
        }

        private static void AssertExact(long a, long b, long c)
        {
            var expected = (long)BigInteger.Divide(new BigInteger(a) * b, c);
            Assert.AreEqual(expected, FluidMulDiv.MulDiv(a, b, c), $"{a} * {b} / {c}");
        }

        private static ulong RandomBits(Random random, int bits)
        {
            var buffer = new byte[8];
            random.NextBytes(buffer);
            var value = BitConverter.ToUInt64(buffer, 0);
            return value & ((1UL << bits) - 1);
        }

        private static long RandomLong(Random random, int bits)
        {
            var magnitude = (long)RandomBits(random, bits);
            return random.Next(2) == 0 ? magnitude : -magnitude;
        }
    }
}
