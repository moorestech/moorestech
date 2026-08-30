using System;
using Client.Game.InGame.BlockSystem.PlaceSystem.Feedback;
using Client.Game.InGame.BlockSystem.PlaceSystem.Util;
using Client.Localization;
using Core.Master;
using Mooresmaster.Localization.Generated;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;

namespace Client.Tests.PlaceSystem.Feedback
{
    /// <summary>
    ///     不足素材行を積む関門が、複数の出所から来た同一アイテムを1行に畳むことを検証する
    ///     Verifies the shortage-line gate folds the same item coming from several sources into one line
    /// </summary>
    public class PlacementFeedbackMaterialShortageTest
    {
        private static readonly Guid Material1Guid = Guid.Parse("00000000-0000-0000-1234-000000000003");
        private static readonly Guid Material2Guid = Guid.Parse("00000000-0000-0000-1234-000000000004");

        [SetUp]
        public void SetUp()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));

            // 不足素材行はアイテム名を表示言語で解決するため実辞書を通す
            // The shortage line resolves the item name in the display language, so go through the real dictionary
            Localize.Initialize();
        }

        [Test]
        // 建設コスト分と予約込みの接続コスト分が別々に積まれても、行は1本で必要数は大きい方
        // Construction and reservation-inclusive connection costs push separately, yet one line remains with the larger requirement
        public void 同一アイテムの不足行は1行に畳み必要数は大きい方を採る()
        {
            var itemId = MasterHolder.ItemMaster.GetItemId(Material1Guid);
            var feedback = new PlacementFeedback();

            feedback.AddMaterialShortages(new[] { new ConstructionMaterialShortage(itemId, 0, 10) });
            feedback.AddMaterialShortages(new[] { new ConstructionMaterialShortage(itemId, 0, 11) });

            Assert.AreEqual(1, feedback.Lines.Count);
            Assert.AreEqual(LocalizationKeys.Ui.Tooltip.PlaceMaterialShortage.Key, feedback.Lines[0].Key.Key);
            Assert.AreEqual(Localize.GetContent(ContentLocalizationKeys.ItemName(Material1Guid)), feedback.Lines[0].TextParams[0]);
            Assert.AreEqual("0", feedback.Lines[0].TextParams[1]);
            Assert.AreEqual("11", feedback.Lines[0].TextParams[2]);
        }

        [Test]
        // 大きい方が先に積まれても結果は変わらない（合算して21にはしない）
        // The order does not matter and the counts are never summed into 21
        public void 大きい必要数が先でも畳んだ結果は変わらない()
        {
            var itemId = MasterHolder.ItemMaster.GetItemId(Material1Guid);
            var feedback = new PlacementFeedback();

            feedback.AddMaterialShortages(new[] { new ConstructionMaterialShortage(itemId, 0, 11) });
            feedback.AddMaterialShortages(new[] { new ConstructionMaterialShortage(itemId, 0, 10) });

            Assert.AreEqual(1, feedback.Lines.Count);
            Assert.AreEqual("11", feedback.Lines[0].TextParams[2]);
        }

        [Test]
        // レール橋脚の建設コスト(鉄板3)と接続レールの素材(鉄板8)も同じアイテムなので1行になる
        // The pier's construction cost (3 plates) and the rail's material (8 plates) are the same item, so they become one line
        public void レール橋脚と接続レールで重なるアイテムも1行に畳む()
        {
            var plateItemId = MasterHolder.ItemMaster.GetItemId(Material1Guid);
            var otherItemId = MasterHolder.ItemMaster.GetItemId(Material2Guid);
            var feedback = new PlacementFeedback();

            feedback.AddMaterialShortages(new[] { new ConstructionMaterialShortage(plateItemId, 0, 3) });
            feedback.AddMaterialShortagesOrFallback(new[]
            {
                new ConstructionMaterialShortage(otherItemId, 1, 12),
                new ConstructionMaterialShortage(plateItemId, 0, 8),
            }, LocalizationKeys.Ui.Tooltip.PlaceRailFailed);

            // 鉄板は先に積んだ位置のまま8へ書き換わり、別素材の行はその後に続く
            // The plate keeps its original position and is rewritten to 8, with the other material following it
            Assert.AreEqual(2, feedback.Lines.Count);
            Assert.AreEqual(Localize.GetContent(ContentLocalizationKeys.ItemName(Material1Guid)), feedback.Lines[0].TextParams[0]);
            Assert.AreEqual("8", feedback.Lines[0].TextParams[2]);
            Assert.AreEqual(Localize.GetContent(ContentLocalizationKeys.ItemName(Material2Guid)), feedback.Lines[1].TextParams[0]);
            Assert.AreEqual("12", feedback.Lines[1].TextParams[2]);
        }

        [Test]
        // 所持数が食い違ったら集めるべき量を過小に見せない小さい方を採る
        // On a mismatched held count the smaller one wins so the amount left to gather is never understated
        public void 所持数が食い違うときは小さい方を採る()
        {
            var itemId = MasterHolder.ItemMaster.GetItemId(Material1Guid);
            var feedback = new PlacementFeedback();

            feedback.AddMaterialShortages(new[] { new ConstructionMaterialShortage(itemId, 5, 10) });
            feedback.AddMaterialShortages(new[] { new ConstructionMaterialShortage(itemId, 2, 10) });

            Assert.AreEqual(1, feedback.Lines.Count);
            Assert.AreEqual("2", feedback.Lines[0].TextParams[1]);
        }

        [Test]
        // 不足が1件も無いときだけ汎用の不可文言へ落ちる
        // Only a completely empty shortage falls back to the generic wording
        public void 不足が空のときは汎用の不可行1行になる()
        {
            var feedback = new PlacementFeedback();

            feedback.AddMaterialShortagesOrFallback(Array.Empty<ConstructionMaterialShortage>(), LocalizationKeys.Ui.Tooltip.PlaceRailFailed);

            Assert.AreEqual(1, feedback.Lines.Count);
            Assert.AreEqual(LocalizationKeys.Ui.Tooltip.PlaceRailFailed.Key, feedback.Lines[0].Key.Key);
        }

        [Test]
        // Clearは畳み先の記録も捨てる。残っていると次フレームの行が前フレームの位置へ書き込まれる
        // Clear drops the fold bookkeeping too; leaving it would write the next frame's line into the previous frame's slot
        public void Clearすると次フレームの不足行は前フレームへ畳まれない()
        {
            var itemId = MasterHolder.ItemMaster.GetItemId(Material1Guid);
            var feedback = new PlacementFeedback();

            feedback.AddMaterialShortages(new[] { new ConstructionMaterialShortage(itemId, 0, 11) });
            feedback.Clear();
            feedback.AddMaterialShortages(new[] { new ConstructionMaterialShortage(itemId, 0, 4) });

            Assert.AreEqual(1, feedback.Lines.Count);
            Assert.AreEqual("4", feedback.Lines[0].TextParams[2]);
        }
    }
}
