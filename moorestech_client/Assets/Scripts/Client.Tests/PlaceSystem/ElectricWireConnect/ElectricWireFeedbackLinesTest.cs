using Client.Game.InGame.BlockSystem.PlaceSystem.ElectricWireConnect.Parts.Feedback;
using Mooresmaster.Localization.Generated;
using NUnit.Framework;

namespace Client.Tests.PlaceSystem.ElectricWireConnect
{
    /// <summary>
    ///     電線ドメインの行生成を検証
    ///     Verify the electric-wire domain's line construction
    /// </summary>
    public class ElectricWireFeedbackLinesTest
    {
        [Test]
        public void 電線コスト0以下は行を作らない()
        {
            Assert.IsFalse(ElectricWireFeedbackLines.TryWireCost(0, out _));
            Assert.IsFalse(ElectricWireFeedbackLines.TryWireCost(-1, out _));

            Assert.IsTrue(ElectricWireFeedbackLines.TryWireCost(3, out var line));
            Assert.AreEqual(LocalizationKeys.Ui.Tooltip.PlaceWireCost.Key, line.Key.Key);
            CollectionAssert.AreEqual(new[] { "3" }, line.TextParams);
        }

        [Test]
        public void 電線不足行は失敗理由写像と同じキーを使う()
        {
            Assert.AreEqual(LocalizationKeys.Ui.Tooltip.PlaceWireNoWireItem.Key, ElectricWireFeedbackLines.WireShortage().Key.Key);
        }

        [Test]
        public void 接続範囲外案内は専用キーを使う()
        {
            Assert.AreEqual(LocalizationKeys.Ui.Tooltip.PlaceWireOutOfRangeNotice.Key, ElectricWireFeedbackLines.WireOutOfRangeNotice().Key.Key);
        }
    }
}
