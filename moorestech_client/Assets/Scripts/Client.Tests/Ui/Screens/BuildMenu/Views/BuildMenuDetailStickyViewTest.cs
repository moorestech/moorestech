using System;
using System.Collections.Generic;
using Client.Localization;
using Client.Tests.Ui.Screens.BuildMenu.Support;
using Client.Ui.Foundation.Tooltip;
using Client.Ui.State.BuildMenu.Entries;
using Client.Ui.State.BuildMenu.Logic;
using Client.Ui.State.Text;
using Mooresmaster.Localization.Generated;
using NUnit.Framework;
using Tests.Module.TestMod;
using UnityEngine.UIElements;
using static Client.Tests.Ui.Screens.BuildMenu.Support.BuildMenuSamples;

namespace Client.Tests.Ui.Screens.BuildMenu.Views
{
    public class BuildMenuDetailStickyViewTest : BuildMenuScreenTestHost
    {
        /// <web-test file="e2e/tests/regression/buildMenu.spec.ts">詳細サイドバーはホバー後にstickyで残る</web-test>
        [Test]
        public void 詳細はホバーした後に検索欄へ移ってもそのエントリを出し続ける()
        {
            OpenMenu(Create(new[] { Material(ForUnitTestItemId.ItemId1, 2, 5) }));
            StringAssert.Contains(new UiText(LocalizationKeys.Ui.BuildMenu.DetailHint).Resolve(), TextOf(Detail));

            Hover(Entry("block", WoodChest));
            StringAssert.Contains("木のチェスト", TextOf(Detail));
            StringAssert.Contains(new UiText(LocalizationKeys.Ui.BuildMenu.RequiredItems).Resolve(), TextOf(Detail));

            // 検索欄へ退いても sticky のまま
            // Stays sticky after moving to the search field
            Hover(Find<TextField>(View, "build-menu-search"));
            StringAssert.Contains("木のチェスト", TextOf(Detail));
        }

        /// <web-test file="e2e/tests/regression/buildMenu.spec.ts">残り設置数は1セット複数個のエントリだけに表示される</web-test>
        [Test]
        public void 残り設置数は1セット複数個のエントリだけに出す()
        {
            OpenMenu(Create(new[] { Material(ForUnitTestItemId.ItemId1, 2, 5) }));
            var perSet = new UiText(LocalizationKeys.Ui.BuildMenu.RequiredItemsPerSet, new[] { new LocalizationTextParam("count", "3") }).Resolve();

            Hover(Entry("block", BeltConveyor));
            StringAssert.Contains("2", Find<TextElement>(View, "build-menu-remaining-placements").text);
            StringAssert.Contains(perSet, TextOf(Detail));

            Hover(Entry("block", WoodChest));
            Assert.IsNull(View.Q("build-menu-remaining-placements"));
            StringAssert.Contains(new UiText(LocalizationKeys.Ui.BuildMenu.RequiredItems).Resolve(), TextOf(Detail));
            StringAssert.DoesNotContain(perSet, TextOf(Detail));
        }

        [Test]
        public void 開いたまま素材が変わるとマスの印と詳細を引き直し同じ要素を使い回す()
        {
            OpenMenu(Create(new[] { Material(ForUnitTestItemId.ItemId1, 2, 1) }));
            var entryBefore = Entry("block", WoodChest);
            Hover(entryBefore);
            var detailBefore = TextOf(Detail);
            Assert.IsTrue(HoverTooltipMark.TryFindMarked(entryBefore, out _, out _));

            // 開いたまま足りた VM を再配信する（Model が組み直した直後と同じ）
            // Rebroadcast a satisfied view model while open (same as right after the model rebuilds)
            View.Render(Create(new[] { Material(ForUnitTestItemId.ItemId1, 2, 5) }));
            Settle();

            Assert.AreSame(entryBefore, Entry("block", WoodChest));
            Assert.IsFalse(HoverTooltipMark.TryFindMarked(Entry("block", WoodChest), out _, out _));
            Assert.AreNotEqual(detailBefore, TextOf(Detail));
            StringAssert.Contains("木のチェスト", TextOf(Detail));
        }

        [Test]
        public void 開いたままホバー中のエントリが消えると詳細はヒントへ戻る()
        {
            OpenMenu(Create(new[] { Material(ForUnitTestItemId.ItemId1, 2, 5) }));
            Hover(Entry("block", WoodChest));
            StringAssert.Contains("木のチェスト", TextOf(Detail));

            // 再配信後に別のマスの下へ入ったポインタが新しいホバーを起こさないよう、検索欄へ退いておく
            // Move to the search field so the pointer cannot enter another slot after the rebroadcast and start a new hover
            Hover(Find<TextField>(View, "build-menu-search"));

            // 木のチェストを含まない VM を開いたまま再配信する
            // Rebroadcast a view model without the wood chest while open
            var names = new Dictionary<Guid, UiText> { [Sub] = UiText.FromVerbatim("General"), [Logistics] = UiText.FromVerbatim("Logistics") };
            var entries = new[] { BuildMenuSamples.Block(IronChest, Logistics, "鉄のチェスト", Array.Empty<BuildMenuMaterialRow>(), false, BuildMenuSetPlacementNone.Instance) };
            View.Render(new BuildMenuViewModel(new[] { new BuildMenuCategory(Logistics, new[] { Sub }) }, names, entries));
            Settle();

            StringAssert.Contains(new UiText(LocalizationKeys.Ui.BuildMenu.DetailHint).Resolve(), TextOf(Detail));
            StringAssert.DoesNotContain("木のチェスト", TextOf(Detail));
        }
    }
}
