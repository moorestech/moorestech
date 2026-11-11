using System.Linq;
using Client.Tests.Ui.Screens.BuildMenu.Support;
using Client.Ui.State.Commands;
using NUnit.Framework;
using UnityEngine.UIElements;
using static Client.Tests.Ui.Screens.BuildMenu.Support.BuildMenuSamples;

namespace Client.Tests.Ui.Screens.BuildMenu.Views
{
    public class BuildMenuViewTest : BuildMenuScreenTestHost
    {
        /// <web-test file="e2e/tests/regression/buildMenu.spec.ts">ui_stateでビルドメニューを開閉し全カテゴリのエントリが1本スクロールに載る</web-test>
        [Test]
        public void 開くと全カテゴリのエントリが1本のスクロールに載り閉じると隠れる()
        {
            OpenMenu(Create());

            Assert.IsTrue(IsShown(View));
            Assert.IsNotNull(Find<VisualElement>(View, "build-menu-panel"));
            Assert.IsTrue(Scroll.contentContainer.Contains(Entry("block", WoodChest)));
            Assert.IsTrue(Scroll.contentContainer.Contains(Entry("trainCar", CargoCar)));

            CloseMenu();
            Assert.IsFalse(IsShown(View));
        }

        /// <web-test file="e2e/tests/regression/buildMenu.spec.ts">エントリ選択とBP右クリック削除のアクション契約</web-test>
        [Test]
        public void エントリのタップは選択しBPの右押下は削除を要求する()
        {
            OpenMenu(Create());

            simulate.Click(Entry("block", WoodChest));
            simulate.Click(CategoryButton(Blueprint));
            Settle();
            simulate.Click(Entry("blueprint", StarterBase), MouseButton.RightMouse);
            Settle();

            CollectionAssert.AreEqual(new[] { WoodChest }, Commands.Selected);
            CollectionAssert.AreEqual(new[] { StarterBase }, Commands.DeletedBlueprints);
            Assert.AreEqual(0, ShownToasts.Count);
        }

        /// <web-test file="e2e/tests/regression/buildMenu.spec.ts">閉じるボタンはGameScreen遷移を要求する</web-test>
        [Test]
        public void 閉じるボタンはGameScreenへの遷移を要求する()
        {
            OpenMenu(Create());

            simulate.Click(Find<Button>(View, "build-menu-close"));

            // 遷移の結果パネルが隠れることは録画 uitk/uitk-build-menu が見る（D-27）
            // The panel hiding after the transition is checked by the uitk/uitk-build-menu recording (D-27)
            Assert.AreEqual(1, Commands.CloseCount);
        }

        /// <web-test file="e2e/tests/regression/buildMenu.spec.ts">エントリの無いカテゴリはサイドバーに出ない</web-test>
        [Test]
        public void エントリの無いカテゴリはサイドバーに出ない()
        {
            OpenMenu(Create());

            Assert.AreEqual(10, CategoryButtons().Length);
            Assert.IsNull(View.Q<Button>($"build-menu-category-{EmptyCategory:D}"));
        }

        [Test]
        public void 良性でない削除の失敗だけをトーストする()
        {
            OpenMenu(Create());
            simulate.Click(CategoryButton(Blueprint));
            Settle();

            // BP が既に無いのは良性（Web の BENIGN_ERRORS）。要求の失敗はトーストする
            // A blueprint already gone is benign (the Web BENIGN_ERRORS); a failed request is toasted
            Commands.SetDeleteResult(new UiCommandRejected(UiCommandRejectReason.BlueprintDeleteNotFound));
            simulate.Click(Entry("blueprint", StarterBase), MouseButton.RightMouse);
            Settle();
            Assert.AreEqual(0, ShownToasts.Count);

            Commands.SetDeleteResult(new UiCommandRejected(UiCommandRejectReason.BlueprintDeleteRequestFailed));
            simulate.Click(Entry("blueprint", StarterBase), MouseButton.RightMouse);
            Settle();
            Assert.AreEqual(1, ShownToasts.Count);
        }
    }
}
