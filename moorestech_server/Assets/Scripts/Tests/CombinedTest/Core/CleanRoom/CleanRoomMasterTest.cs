using System;
using Core.Master;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;

namespace Tests.CombinedTest.Core.CleanRoom
{
    public class CleanRoomMasterTest
    {
        [Test]
        public void ThresholdsLoadedInOrderTest()
        {
            var (_, _) = new MoorestechServerDIContainerGenerator()
                .Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));

            var master = MasterHolder.CleanRoomMaster;
            Assert.IsTrue(master.IsAvailable);
            Assert.AreEqual(4, master.Thresholds.Count);
            // 良い順（A→D）で並び、濃度上限が単調増加であること
            // Rows are ordered best-to-worst with monotonically increasing concentration caps
            Assert.AreEqual(10.0, master.Thresholds[0].MaxConcentration, 0.0001);
            Assert.AreEqual(1000.0, master.Thresholds[3].MaxConcentration, 0.0001);
            Assert.AreEqual(4, master.OutThresholdIndex);
        }

        [Test]
        public void EmptyMasterFallbackTest()
        {
            var empty = CleanRoomMaster.CreateEmpty();
            Assert.IsFalse(empty.IsAvailable);
            Assert.AreEqual(0, empty.Thresholds.Count);
        }

        [Test]
        public void TryGetThresholdIndexByClassNameTest()
        {
            var (_, _) = new MoorestechServerDIContainerGenerator()
                .Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));

            var master = MasterHolder.CleanRoomMaster;
            // 既存クラス名は行順に依存せず自身のインデックスへ解決される
            // An existing class name resolves to its own index independently of row order
            Assert.IsTrue(master.TryGetThresholdIndexByClassName("A", out var indexA));
            Assert.AreEqual(0, indexA);
            Assert.IsTrue(master.TryGetThresholdIndexByClassName("C", out var indexC));
            Assert.AreEqual(2, indexC);
            Assert.IsFalse(master.TryGetThresholdIndexByClassName("Z", out _));
        }

        [Test]
        public void TryGetChipDrawTest()
        {
            var (_, _) = new MoorestechServerDIContainerGenerator()
                .Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));

            var master = MasterHolder.CleanRoomMaster;
            // テストModのTestChipRecipeに紐づく抽選テーブルが引けること
            // The draw table bound to the test mod's TestChipRecipe must be resolvable
            var recipeGuid = Guid.Parse("19b0d248-0ce5-4e5f-b59c-5897177b6268");
            Assert.IsTrue(master.TryGetChipDraw(recipeGuid, out var chipDraw));
            Assert.AreEqual(0.8f, chipDraw.EuvSuccessRate, 0.0001f);
            Assert.IsFalse(master.TryGetChipDraw(Guid.NewGuid(), out _));
        }
    }
}
