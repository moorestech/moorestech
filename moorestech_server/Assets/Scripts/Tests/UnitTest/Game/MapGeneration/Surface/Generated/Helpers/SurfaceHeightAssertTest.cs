using System;
using NUnit.Framework;

namespace Tests.UnitTest.Game.MapGeneration.Surface.Generated.Helpers
{
    public class SurfaceHeightAssertTest
    {
        [Test]
        public void SpecialValuesMatchNUnitExactNumericEquality()
        {
            // NaNの符号・payloadとゼロの符号は元のNUnit同様に区別しない
            // Like the original NUnit comparison, ignore NaN sign/payload and zero sign
            float payloadNaN = BitConverter.ToSingle(BitConverter.GetBytes(0xffc00001u), 0);
            var expected = new[] { 0f, float.NaN, float.PositiveInfinity, float.NegativeInfinity, 1f };
            var actual = new[] { -0f, payloadNaN, float.PositiveInfinity, float.NegativeInfinity, 1f };
            CollectionAssert.AreEqual(expected, actual);
            SurfaceHeightAssert.AreEqual(expected, actual, "special values");
            SurfaceHeightAssert.AreEqual(new[,] { { 0f, float.NaN } }, new[,] { { -0f, payloadNaN } }, "matrix special values");
        }

        [Test]
        public void VectorCountsEveryMismatchIncludingLastElement()
        {
            var expected = new[] { 0f, 1f, 2f, 3f };
            var actual = new[] { 0f, 1.0000001f, 2f, -3f };
            Assert.That(actual, Is.Not.EqualTo(expected));
            var error = Assert.Throws<AssertionException>(() => SurfaceHeightAssert.AreEqual(expected, actual, "vector"));
            Assert.That(error.Message, Does.Contain("mismatches=2, firstIndex=1"));
            Assert.That(error.Message, Does.Contain($"expected={expected[1]:R}, actual={actual[1]:R}"));
        }

        [Test]
        public void MatrixCountsEveryMismatchAcrossRowsAndLastColumn()
        {
            var expected = new[,] { { 0f, 1f, 2f }, { 3f, 4f, 5f } };
            var actual = new[,] { { 0f, 1f, -2f }, { 3f, 4f, -5f } };
            Assert.That(actual, Is.Not.EqualTo(expected));
            var error = Assert.Throws<AssertionException>(() => SurfaceHeightAssert.AreEqual(expected, actual, "matrix"));
            Assert.That(error.Message, Does.Contain("mismatches=2, firstIndex=2"));
            Assert.That(error.Message, Does.Contain("expected=2, actual=-2"));
        }

        [Test]
        public void ShapeDifferencesAndNonFiniteMismatchesFail()
        {
            // 長さ・形状と非有限値の取り違えを丸めや許容誤差で隠さない
            // Do not hide length, shape, or nonfinite differences with rounding or tolerance
            Assert.Throws<AssertionException>(() => SurfaceHeightAssert.AreEqual(new float[2], new float[1], "length"));
            Assert.Throws<AssertionException>(() => SurfaceHeightAssert.AreEqual(new float[2, 3], new float[3, 2], "shape"));
            Assert.Throws<AssertionException>(() => SurfaceHeightAssert.AreEqual(new float[2, 3], new float[2, 2], "columns"));
            var expected = new[] { float.NaN, float.PositiveInfinity };
            var actual = new[] { 0f, float.NegativeInfinity };
            Assert.That(actual, Is.Not.EqualTo(expected));
            var error = Assert.Throws<AssertionException>(() => SurfaceHeightAssert.AreEqual(expected, actual, "nonfinite"));
            Assert.That(error.Message, Does.Contain("mismatches=2, firstIndex=0"));
        }
    }
}
