using Client.Game.InGame.Tutorial.UIHighlight;
using Client.Ui.Components.Controls;
using Client.Ui.Components.Slots;
using Client.Ui.Foundation.Anchor;
using Client.Ui.Screens.Recipe.Parts;
using Client.Ui.State.Recipe.Screen;
using Core.Master;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;

namespace Client.Tests.Ui.Screens.Recipe.ItemList
{
    /// <summary>
    /// 一覧のパネルの並び・バッジ・選択・隠したときの戻りを確かめる（L1）
    /// Verifies the list panel's order, badges, selection and reset on hide (L1)
    /// </summary>
    public class ItemListViewTest : ItemListTestHost
    {
        /// <web-test file="e2e/tests/recipe/recipe.spec.ts">リスト上をドラッグしてもアイテム選択は変わらない（スクロール操作扱い）</web-test>
        [Test]
        public void 選択中に一覧をドラッグしても選択は変わらない()
        {
            var view = MountList(ListOf(MaxItemId));
            Selection.Select(new ItemId(5));

            var at = view.Q<ItemListCellView>("item-list-cell-9").worldBound.center;
            PressMoveRelease(at, at + new Vector2(0, -40));
            Assert.Greater(view.Q<GameScrollView>("item-list-scroll").scrollOffset.y, 0f, "the drag must actually scroll");
            Assert.AreEqual(new ItemId(5), ((ItemRecipeSelection)Selection.Current.Value).ItemId);
        }

        /// <web-test file="e2e/tests/recipe/recipe.spec.ts">クラフト可能数0のアイテムは個数バッジを出さない</web-test>
        [Test]
        public void クラフト可能数0はバッジを出さず正の数は出す()
        {
            var view = MountList(new ItemListViewModel(new[] { new ItemListCell(new ItemId(101), 0), new ItemListCell(new ItemId(100), 3) }));

            var counted = view.Q<ItemListCellView>("item-list-cell-100").Q<SlotContent>();
            Assert.IsTrue(counted.ClassListContains("slot-content--counted"));
            Assert.AreEqual("3", counted.Q<Label>(className: "outlined-count-label--glyph").text);
            Assert.AreEqual(DisplayStyle.Flex, counted.Q<Label>(className: "outlined-count-label--glyph").resolvedStyle.display);

            // 0 個は数の印もラベルの字も持たない
            // Zero carries neither the counted mark nor any label text
            var zero = view.Q<ItemListCellView>("item-list-cell-101").Q<SlotContent>();
            Assert.IsFalse(zero.ClassListContains("slot-content--counted"));
            Assert.IsTrue(string.IsNullOrEmpty(zero.Q<Label>(className: "outlined-count-label--glyph").text));
        }

        [Test]
        public void 隠すとスクロールが先頭へ戻る()
        {
            // Web は画面を出るとアンマウントするので、開き直すと先頭から
            // The Web unmounts on leaving the screen, so reopening starts at the top
            var view = MountList(ListOf(MaxItemId));
            var scroll = view.Q<GameScrollView>("item-list-scroll");
            scroll.SetScrollOffset(100);
            simulate.FrameUpdate();
            Assert.AreEqual(100f, scroll.scrollOffset.y, 0.5f);

            // 隠した時点で戻す（UITK の視口が潰れる丸めに頼らない）
            // Reset at hide time (not relying on UITK clamping once the viewport collapses)
            view.SetShown(false);
            Assert.AreEqual(0f, scroll.scrollOffset.y, 0.01f);
            simulate.FrameUpdate();
            view.SetShown(true);
            simulate.FrameUpdate();
            simulate.FrameUpdate();
            Assert.AreEqual(DisplayStyle.Flex, view.resolvedStyle.display);
            Assert.AreEqual(0f, scroll.scrollOffset.y, 0.01f);
        }

        [Test]
        public void 六列で並びアンカーがセルごとに付き同じ並びの描き直しでセルを作り直さない()
        {
            var view = MountList(ListOf(30));

            // 7 件目は次の段の先頭
            // The seventh cell opens the next row
            var first = view.Q<ItemListCellView>("item-list-cell-1");
            var sixth = view.Q<ItemListCellView>("item-list-cell-6");
            var seventh = view.Q<ItemListCellView>("item-list-cell-7");
            Assert.AreEqual(first.worldBound.y, sixth.worldBound.y, 0.5f);
            Assert.Greater(seventh.worldBound.y, first.worldBound.yMax);
            Assert.AreEqual(first.worldBound.x, seventh.worldBound.x, 0.5f);

            var resolved = TutorialAnchorRegistry.Resolve(view, AnchorIds.RecipeItem(3));
            Assert.IsInstanceOf<TutorialAnchorResolution.Unique>(resolved);
            Assert.AreSame(view.Q<ItemListCellView>("item-list-cell-3"), ((TutorialAnchorResolution.Unique)resolved).Element);

            // 同じ ID の列なら数が変わってもセルはそのまま
            // The same ID order keeps the cells even when counts change
            var cells = new ItemListCell[30];
            for (var i = 0; i < 30; i++) cells[i] = new ItemListCell(new ItemId(i + 1), 2);
            view.Render(new ItemListViewModel(cells));
            Assert.AreSame(first, view.Q<ItemListCellView>("item-list-cell-1"));
            Assert.IsTrue(first.Q<SlotContent>().ClassListContains("slot-content--counted"));
        }
    }
}
