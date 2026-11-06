using Client.Tests.Ui.TestDoubles;
using Client.Ui.State.BlockInventory.Logic.CleanRoom;
using Mooresmaster.Localization.Generated;
using NUnit.Framework;

namespace Client.Tests.Ui.State.BlockInventory.CleanRoom
{
    /// <summary>
    /// Web の cleanRoomYieldLogic.test.ts の写し（L0）
    /// Port of Web cleanRoomYieldLogic.test.ts (L0)
    /// </summary>
    public class CleanRoomYieldLogicTest
    {
        /// <web-test file="src/features/blockInventory/details/cleanRoom/__tests__/cleanRoomYieldLogic.test.ts">EUV は層の失敗率だけを出す</web-test>
        [Test]
        public void 露光は層の失敗率だけを出す()
        {
            var lines = CleanRoomYieldLogic.Lines(CleanRoomYieldFetched.WithoutPendingDraw(new LayerExposureYieldRates(0.02)));
            Assert.AreEqual(1, lines.Count);
            Assert.AreEqual("clean-room-yield-layer-fail-rate", lines[0].TestId);
            UiTextAssert.AreEqual(LocalizationKeys.Ui.BlockInventory.CleanRoomYieldLayerFailRate, new[] { "percent", "2.0" }, lines[0].Text);
        }

        /// <web-test file="src/features/blockInventory/details/cleanRoom/__tests__/cleanRoomYieldLogic.test.ts">ダイシングは品質Tier・損失率・0でないレベル割合・失敗割合を出す</web-test>
        [Test]
        public void ダイシングは品質Tierと損失率と0でないレベル割合と失敗割合を出す()
        {
            var lines = CleanRoomYieldLogic.Lines(CleanRoomYieldFetched.WithoutPendingDraw(new DicingYieldRates(0.03, 1, new[] { 0.7, 0.1, 0 }, 0.2)));
            Assert.AreEqual(5, lines.Count);
            AssertLine(lines[0], "clean-room-yield-quality-tier", LocalizationKeys.Ui.BlockInventory.CleanRoomYieldQualityTier, new[] { "tier", "1" });
            AssertLine(lines[1], "clean-room-yield-die-loss-rate", LocalizationKeys.Ui.BlockInventory.CleanRoomYieldDieLossRate, new[] { "percent", "3.0" });
            AssertLine(lines[2], "clean-room-yield-level-rate-1", LocalizationKeys.Ui.BlockInventory.CleanRoomYieldLevelRate, new[] { "level", "1", "percent", "70.0" });
            AssertLine(lines[3], "clean-room-yield-level-rate-2", LocalizationKeys.Ui.BlockInventory.CleanRoomYieldLevelRate, new[] { "level", "2", "percent", "10.0" });
            AssertLine(lines[4], "clean-room-yield-failed-rate", LocalizationKeys.Ui.BlockInventory.CleanRoomYieldFailedRate, new[] { "percent", "20.0" });
        }

        /// <web-test file="src/features/blockInventory/details/cleanRoom/__tests__/cleanRoomYieldLogic.test.ts">出力待ちの確定済み抽選があれば、確定時の値と次の新規抽選の現在値を見出し付きで分けて出す</web-test>
        [Test]
        public void 出力待ちの確定済み抽選があれば確定時の値と次の抽選の値を見出し付きで分けて出す()
        {
            var next = new DicingYieldRates(0.15, 0, new[] { 0.2, 0, 0 }, 0.8);
            var pending = new DicingYieldRates(0.03, 1, new[] { 0.7, 0.1, 0 }, 0.2);
            var lines = CleanRoomYieldLogic.Lines(CleanRoomYieldFetched.WithPendingDraw(next, pending));
            var expected = new[]
            {
                ("clean-room-yield-pending-label", new string[0]),
                ("clean-room-yield-pending-quality-tier", new[] { "tier", "1" }),
                ("clean-room-yield-pending-die-loss-rate", new[] { "percent", "3.0" }),
                ("clean-room-yield-pending-level-rate-1", new[] { "level", "1", "percent", "70.0" }),
                ("clean-room-yield-pending-level-rate-2", new[] { "level", "2", "percent", "10.0" }),
                ("clean-room-yield-pending-failed-rate", new[] { "percent", "20.0" }),
                ("clean-room-yield-next-label", new string[0]),
                ("clean-room-yield-quality-tier", new[] { "tier", "0" }),
                ("clean-room-yield-die-loss-rate", new[] { "percent", "15.0" }),
                ("clean-room-yield-level-rate-1", new[] { "level", "1", "percent", "20.0" }),
                ("clean-room-yield-failed-rate", new[] { "percent", "80.0" }),
            };
            Assert.AreEqual(expected.Length, lines.Count);
            for (var i = 0; i < expected.Length; i++)
            {
                Assert.AreEqual(expected[i].Item1, lines[i].TestId);
                UiTextAssert.AreEqual(lines[i].Text.Key.Key, expected[i].Item2, lines[i].Text);
            }
        }

        /// <web-test file="src/features/blockInventory/details/cleanRoom/__tests__/cleanRoomYieldLogic.test.ts">割合は小数1桁の百分率に丸める</web-test>
        [Test]
        public void 割合は小数1桁の百分率に丸める()
        {
            var lines = CleanRoomYieldLogic.Lines(CleanRoomYieldFetched.WithoutPendingDraw(new LayerExposureYieldRates(0.12345)));
            UiTextAssert.AreEqual(LocalizationKeys.Ui.BlockInventory.CleanRoomYieldLayerFailRate, new[] { "percent", "12.3" }, lines[0].Text);
        }

        private static void AssertLine(CleanRoomYieldLine line, string testId, LocalizationKey key, string[] textParams)
        {
            Assert.AreEqual(testId, line.TestId);
            UiTextAssert.AreEqual(key, textParams, line.Text);
        }
    }
}
