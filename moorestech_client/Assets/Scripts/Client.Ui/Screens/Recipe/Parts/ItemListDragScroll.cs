using Client.Ui.Components.Controls;
using Client.Ui.Foundation.Input.Gesture;
using Client.Ui.State.Recipe.Screen;
using UnityEngine;
using UnityEngine.UIElements;

namespace Client.Ui.Screens.Recipe.Parts
{
    /// <summary>
    /// 一覧の掴みスクロール（Web useDragScroll の写し）。タップはセルを選び、ドラッグはスクロールだけ
    /// Grab-drag scrolling for the list (port of the Web useDragScroll); a tap selects the cell, a drag only scrolls
    /// </summary>
    public sealed class ItemListDragScroll : IUiPressGestureHandler
    {
        private readonly GameScrollView _scroll;
        private readonly RecipeItemSelection _selection;
        private float _startScrollTop;

        public ItemListDragScroll(GameScrollView scroll, RecipeItemSelection selection)
        {
            _scroll = scroll;
            _selection = selection;
        }

        public void OnPressStarted(Vector2 panelPosition)
        {
            _startScrollTop = _scroll.scrollOffset.y;
        }

        public void OnDragMoved(UiPressMove move)
        {
            // 押した点からの移動ぶんだけ逆向きに送る（内容が指に付いて動く。範囲外は ScrollView が丸める）
            // Scroll opposite to the movement from the press point (content follows the pointer; ScrollView clamps the range)
            _scroll.SetScrollOffset(UiDragThreshold.NextScrollTop(_startScrollTop, move.FromStart.y));
        }

        public void OnDragEnded(Vector2 panelPosition)
        {
        }

        public void OnTapped(VisualElement pressTarget)
        {
            // 押下の対象はセルの子（スロットの枠）が普通だが、セル自身のこともあるので両方を見る
            // The press target is usually a child of the cell (the slot frame) but can be the cell itself, so check both
            var cell = pressTarget as ItemListCellView ?? pressTarget?.GetFirstAncestorOfType<ItemListCellView>();
            // silent-ok: セルの外（隙間・余白）のタップは Web と同じく何もしない / ItemListDragScrollTest がセル上のタップの選択を見る
            if (cell == null) return;
            _selection.Select(cell.ItemId);
        }

        public void OnAborted(bool dragged)
        {
        }
    }
}
