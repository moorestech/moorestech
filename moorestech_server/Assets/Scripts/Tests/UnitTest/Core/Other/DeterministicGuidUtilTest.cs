using System;
using Core.Master;
using NUnit.Framework;

namespace Tests.UnitTest.Core.Other
{
    /// <summary>
    ///     DeterministicGuidUtilの決定性を検証するテスト（DIコンテナ不要）
    ///     Tests verifying the determinism of DeterministicGuidUtil (no DI container required)
    /// </summary>
    public class DeterministicGuidUtilTest
    {
        // 同一seedは同一GUID、異なるseedは異なるGUIDを返すことを検証
        // Same seed yields the same GUID; different seeds yield different GUIDs
        [Test]
        public void DeterministicTest()
        {
            var a1 = DeterministicGuidUtil.Create("base-guid:lv2");
            var a2 = DeterministicGuidUtil.Create("base-guid:lv2");
            var b = DeterministicGuidUtil.Create("base-guid:lv3");

            Assert.AreEqual(a1, a2);
            Assert.AreNotEqual(a1, b);
            Assert.AreNotEqual(Guid.Empty, a1);
        }
    }
}
