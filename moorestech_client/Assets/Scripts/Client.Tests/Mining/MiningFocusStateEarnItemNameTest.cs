using Client.Game.InGame.Mining;
using Client.Localization;
using Core.Master;
using Mooresmaster.Localization.Generated;
using NUnit.Framework;

namespace Client.Tests.Mining
{
    /// <summary>
    ///     取得物を持つ対象へ、名前を前置した文言が結果ごとに選ばれることを検証（ADR 0033）
    ///     Verify the name-prefixed sentence is chosen per outcome for targets that yield items (ADR 0033)
    ///     EditModeではmod辞書が載らないため名前は欠落マーカーへ解決される。ここで固定するのはGUIDの経路と並び順
    ///     No mod dictionary is loaded in EditMode, so names resolve to the miss marker; what is pinned here is GUID routing and ordering
    /// </summary>
    public class MiningFocusStateEarnItemNameTest : MiningFocusStateTestFixture
    {
        [Test]
        public void 取得物のある採掘可能な対象には名前つきの長押し文言を出す()
        {
            var focusState = new MiningFocusState();
            var next = RunFocusState(MiningStartOutcome.Ready, focusState, new[] { EarnItemGuid });

            Assert.AreSame(focusState, next);
            Assert.AreEqual(LocalizationKeys.Ui.Tooltip.NamedMineHold.Key, ShownTooltipKey());
            CollectionAssert.AreEqual(
                new[] { Localize.GetContent(ContentLocalizationKeys.ItemName(EarnItemGuid)) },
                ShownTooltipParams());
        }

        [Test]
        public void 取得物のあるPickUp対象には名前つきの単クリック文言を出す()
        {
            var focusState = new MiningFocusState();
            var next = RunFocusState(MiningStartOutcome.InstantPickUp, focusState, new[] { EarnItemGuid });

            Assert.AreSame(focusState, next);
            Assert.AreEqual(LocalizationKeys.Ui.Tooltip.NamedMineClick.Key, ShownTooltipKey());
        }

        [Test]
        public void 取得物のある手掘り不可の対象には名前つきの不可文言を出す()
        {
            var focusState = new MiningFocusState();
            var next = RunFocusState(MiningStartOutcome.HandMiningNotAllowed, focusState, new[] { EarnItemGuid });

            Assert.AreSame(focusState, next);
            Assert.AreEqual(LocalizationKeys.Ui.Tooltip.NamedCannotHandMine.Key, ShownTooltipKey());
        }

        [Test]
        public void 取得物のある装備不一致の対象には名前を先頭に必要ツールを続ける()
        {
            var focusState = new MiningFocusState();
            var next = RunFocusState(MiningStartOutcome.ToolMismatch, focusState, new[] { EarnItemGuid });

            Assert.AreSame(focusState, next);
            Assert.AreEqual(LocalizationKeys.Ui.Tooltip.NamedRequiredItems.Key, ShownTooltipKey());

            // 名前が{p0}・必要ツールが{p1}
            // Pin the ordering: the name is {p0} and the required tools are {p1}
            var shownParams = ShownTooltipParams();
            Assert.AreEqual(2, shownParams.Count);
            Assert.AreEqual(Localize.GetContent(ContentLocalizationKeys.ItemName(EarnItemGuid)), shownParams[0]);
            Assert.AreEqual(Localize.GetContent(ContentLocalizationKeys.ItemName(ToolItemGuid)), shownParams[1]);

            // 取得物名とツール名が同一文字列なら経路の取り違えを検出できない
            // Identical strings would hide a swapped route, so the two must differ
            Assert.AreNotEqual(shownParams[0], shownParams[1]);
        }
    }
}
