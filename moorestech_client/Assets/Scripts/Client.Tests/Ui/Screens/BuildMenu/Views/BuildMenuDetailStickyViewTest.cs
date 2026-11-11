using Client.Localization;
using Client.Tests.Ui.Screens.BuildMenu.Support;
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
    }
}
