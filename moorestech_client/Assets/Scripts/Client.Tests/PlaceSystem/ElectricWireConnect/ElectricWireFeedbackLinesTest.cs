using System;
using Client.Game.InGame.BlockSystem.PlaceSystem.ElectricWireConnect.Parts.Feedback;
using Client.Game.InGame.BlockSystem.PlaceSystem.Util;
using Client.Localization;
using Core.Master;
using Mooresmaster.Localization.Generated;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;

namespace Client.Tests.PlaceSystem.ElectricWireConnect
{
    /// <summary>
    ///     電線ドメインの行生成を検証
    ///     Verify the electric-wire domain's line construction
    /// </summary>
    public class ElectricWireFeedbackLinesTest
    {
        private static readonly Guid Material1Guid = Guid.Parse("00000000-0000-0000-1234-000000000003");
        private static readonly Guid Material2Guid = Guid.Parse("00000000-0000-0000-1234-000000000004");

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
        public void 電線不足行は不足素材ごとに実アイテム名と所持必要を載せる()
        {
            CreateServer();
            var shortages = new[]
            {
                new ConstructionMaterialShortage(MasterHolder.ItemMaster.GetItemId(Material1Guid), 1, 4),
                new ConstructionMaterialShortage(MasterHolder.ItemMaster.GetItemId(Material2Guid), 0, 2),
            };

            var lines = ElectricWireFeedbackLines.WireShortageLines(shortages);

            Assert.AreEqual(2, lines.Count);
            Assert.AreEqual(LocalizationKeys.Ui.Tooltip.PlaceMaterialShortage.Key, lines[0].Key.Key);
            CollectionAssert.AreEqual(new[] { "1", "4" }, new[] { lines[0].TextParams[1], lines[0].TextParams[2] });
            CollectionAssert.AreEqual(new[] { "0", "2" }, new[] { lines[1].TextParams[1], lines[1].TextParams[2] });
        }

        [Test]
        public void 不足素材が算出できないときは汎用の設置不可行へ落ちる()
        {
            var lines = ElectricWireFeedbackLines.WireShortageLines(Array.Empty<ConstructionMaterialShortage>());

            Assert.AreEqual(1, lines.Count);
            Assert.AreEqual(LocalizationKeys.Ui.Tooltip.PlaceWireFailed.Key, lines[0].Key.Key);
        }

        [Test]
        public void 接続範囲外案内は専用キーを使う()
        {
            Assert.AreEqual(LocalizationKeys.Ui.Tooltip.PlaceWireOutOfRangeNotice.Key, ElectricWireFeedbackLines.WireOutOfRangeNotice().Key.Key);
        }

        private static void CreateServer()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));

            // 不足素材行はアイテム名を表示言語で解決するため実辞書を通す
            // The shortage line resolves the item name in the display language, so go through the real dictionary
            Localize.Initialize();
        }
    }
}
