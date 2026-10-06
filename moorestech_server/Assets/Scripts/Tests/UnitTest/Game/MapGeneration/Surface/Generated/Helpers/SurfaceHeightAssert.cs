using NUnit.Framework;

namespace Tests.UnitTest.Game.MapGeneration.Surface.Generated.Helpers
{
    internal static class SurfaceHeightAssert
    {
        internal static void AreEqual(float[] expected, float[] actual, string context)
        {
            Assert.That(actual.Length, Is.EqualTo(expected.Length), context + " length");
            var differences = new Differences();
            for (int index = 0; index < expected.Length; index++)
                differences.Compare(expected[index], actual[index], index);
            differences.AssertEqual(context);
        }

        internal static void AreEqual(float[,] expected, float[,] actual, string context)
        {
            Assert.That(actual.GetLength(0), Is.EqualTo(expected.GetLength(0)), context + " rows");
            Assert.That(actual.GetLength(1), Is.EqualTo(expected.GetLength(1)), context + " columns");
            var differences = new Differences();
            int width = expected.GetLength(1);

            // 全要素を走査し失敗数と初値を報告
            // Scan every element and report the failure count and first values
            for (int z = 0; z < expected.GetLength(0); z++)
            for (int x = 0; x < width; x++)
                differences.Compare(expected[z, x], actual[z, x], z * width + x);
            differences.AssertEqual(context);
        }

        private struct Differences
        {
            private int _count;
            private int _firstIndex;
            private float _expected;
            private float _actual;

            internal void Compare(float expected, float actual, int index)
            {
                // NUnitの数値完全一致を維持する（符号付きゼロとNaN同士は等しい）
                // Preserve NUnit exact numeric equality: signed zeros and pairs of NaNs are equal
                if (expected == actual || (float.IsNaN(expected) && float.IsNaN(actual))) return;
                if (_count++ != 0) return;
                _firstIndex = index;
                _expected = expected;
                _actual = actual;
            }

            internal void AssertEqual(string context)
            {
                Assert.That(_count, Is.Zero,
                    $"{context}: mismatches={_count}, firstIndex={_firstIndex}, expected={_expected:R}, actual={_actual:R}");
            }
        }
    }
}
