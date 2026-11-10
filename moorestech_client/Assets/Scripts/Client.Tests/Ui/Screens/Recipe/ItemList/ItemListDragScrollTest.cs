using Client.Ui.Components.Controls;
using Client.Ui.Screens.Recipe.Parts;
using Client.Ui.State.Recipe.Screen;
using Core.Master;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;

namespace Client.Tests.Ui.Screens.Recipe.ItemList
{
    /// <summary>
    /// 一覧の掴みスクロールとタップの選択を確かめる（L1）
    /// Verifies the list's grab scrolling and tap selection (L1)
    /// </summary>
    public class ItemListDragScrollTest : ItemListTestHost
    {
        /// <web-test file="src/features/recipe/panels/useDragScroll.test.ts">閾値未満で離すとタップとして押下点を通知する</web-test>
        [Test]
        public void 閾値未満で離すと押したセルのアイテムを選ぶ()
        {
            var view = MountList(ListOf(30));
            var at = view.Q<ItemListCellView>("item-list-cell-3").worldBound.center;

            // 4px は閾値 5 未満
            // 4px is under the threshold of 5
            PressMoveRelease(at, at + new Vector2(0, 4));
            Assert.IsInstanceOf<ItemRecipeSelection>(Selection.Current.Value);
            Assert.AreEqual(new ItemId(3), ((ItemRecipeSelection)Selection.Current.Value).ItemId);
        }

        /// <web-test file="src/features/recipe/panels/useDragScroll.test.ts">閾値を超えて動かすとスクロールし、離してもタップにしない</web-test>
        [Test]
        public void 閾値を超えて動かすとスクロールし離しても選ばない()
        {
            var view = MountList(ListOf(MaxItemId));
            var scroll = view.Q<GameScrollView>("item-list-scroll");
            scroll.SetScrollOffset(100);
            simulate.FrameUpdate();
            Assert.AreEqual(100f, scroll.scrollOffset.y, 0.5f);

            // 上へ 30 動かすと内容が指に付いて 30 送られる
            // Moving up 30 drags the content along, scrolling by 30
            var at = view.Q<ItemListCellView>("item-list-cell-40").worldBound.center;
            PressMoveRelease(at, at + new Vector2(0, -30));
            Assert.AreEqual(130f, scroll.scrollOffset.y, 0.5f);
            Assert.IsInstanceOf<NoRecipeSelection>(Selection.Current.Value);
        }
    }
}
