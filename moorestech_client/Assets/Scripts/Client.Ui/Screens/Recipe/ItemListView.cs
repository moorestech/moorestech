using System.Collections.Generic;
using Client.Ui.Components.Controls;
using Client.Ui.Components.Panels;
using Client.Ui.Components.Slots;
using Client.Ui.Content;
using Client.Ui.Foundation.Input.Gesture;
using Client.Ui.Foundation.Theme;
using Client.Ui.Screens.Recipe.Parts;
using Client.Ui.State.Recipe.Screen;
using Client.Ui.State.Text;
using Mooresmaster.Localization.Generated;
using UnityEngine.UIElements;

namespace Client.Ui.Screens.Recipe
{
    /// <summary>
    /// アイテム一覧のパネル（Web ItemListPanel の写し）。6 列・掴みスクロール・クラフト可能数のバッジ
    /// Item list panel (port of the Web ItemListPanel); six columns, grab scrolling and craftable-count badges
    /// </summary>
    public sealed class ItemListView : VisualElement
    {
        private const int Columns = 6;
        private const string ClipInsetProperty = "--item-list-scroll-clip-inset";
        private const string MarginLeftBaseProperty = "--item-list-scroll-margin-left-base";
        private const string MarginTopBaseProperty = "--item-list-scroll-margin-top-base";

        private readonly GameContentPresentation _content;
        private readonly SlotGrid _grid = new("item-list-grid", Columns);
        private readonly List<ItemListCellView> _cells = new();
        private readonly GameScrollView _scroll;

        public ItemListView(GameContentPresentation content, RecipeItemSelection selection)
        {
            _content = content;
            AddToClassList("item-list");
            var panel = new GamePanel("item-list-panel", GamePanelFrame.Default);
            panel.SetTitle(new UiText(LocalizationKeys.Ui.Recipe.CatalogTitle));

            // 本文いっぱいのスクロール領域と、その視口に掴みスクロールを付ける（バーの上の押下は拾わない）
            // A body-filling scroll area with grab scrolling on its viewport (presses on the bar are not taken)
            _scroll = new GameScrollView("item-list-scroll");
            _scroll.AddToClassList("item-list__scroll");
            _scroll.Add(_grid);
            _scroll.RegisterCallback<CustomStyleResolvedEvent>(_ => ApplyClipClearance());
            new UiPressGesture(_scroll.contentViewport, new ItemListDragScroll(_scroll, selection));
            panel.Add(_scroll);
            Add(panel);
        }

        public void Render(ItemListViewModel vm)
        {
            // 並びが変われば作り直し、同じなら数だけ当て直す
            // Rebuild when the order changes, otherwise only re-apply counts
            if (!SameItems(vm.Cells)) Rebuild(vm.Cells);
            for (var i = 0; i < vm.Cells.Count; i++) _cells[i].SetContent(_content.Item(vm.Cells[i].ItemId), vm.Cells[i].CraftableCount);

            #region Internal

            bool SameItems(IReadOnlyList<ItemListCell> next)
            {
                if (next.Count != _cells.Count) return false;
                for (var i = 0; i < next.Count; i++)
                {
                    if (next[i].ItemId != _cells[i].ItemId) return false;
                }
                return true;
            }

            void Rebuild(IReadOnlyList<ItemListCell> next)
            {
                // 解放で並びが変わったときだけセルを作り直す
                // Recreate the cells only when an unlock changed the order
                _grid.ClearItems();
                _cells.Clear();
                foreach (var cell in next)
                {
                    var view = new ItemListCellView(cell.ItemId);
                    _cells.Add(view);
                    _grid.AddItem(view);
                }
            }

            #endregion
        }

        public void SetShown(bool shown)
        {
            style.display = shown ? DisplayStyle.Flex : DisplayStyle.None;

            // 出すときは何も戻さない（戻すのは閉じる側）
            // Showing resets nothing (the hide side does)
            if (shown) return;

            // 開き直したら先頭から（Web は画面を出るとアンマウントし、スクロール位置を持ち越さない）
            // Reopen at the top (Web unmounts on leaving the screen and does not carry the scroll position)
            _scroll.SetScrollOffset(0f);

            // 掴み中に閉じたら捕捉を手放して掴みを終える（display:none では捕捉が残り、開き直した後の移動で古い掴みが送ってしまう）
            // Closing mid-grab releases the capture to end the grab (display:none keeps the capture, so the stale grab would scroll after reopening)
            for (var pointerId = 0; pointerId < PointerId.maxPointers; pointerId++)
            {
                if (_scroll.contentViewport.HasPointerCapture(pointerId)) _scroll.contentViewport.ReleasePointer(pointerId);
            }
        }

        private void ApplyClipClearance()
        {
            // Web の .scrollArea の calc の margin。USS に calc が無いので C# が当てる（前例 RecipeEntryList）
            // The Web .scrollArea calc margins; USS has no calc, so C# applies them (precedent RecipeEntryList)
            // 読めないときは UssCustomStyleReader が LogError を出し、余白は前の値のまま
            // When unreadable UssCustomStyleReader logs an error and the spacing keeps its previous value
            if (!UssCustomStyleReader.TryReadPixels(_scroll.customStyle, ClipInsetProperty, out var inset)) return;
            if (!UssCustomStyleReader.TryReadPixels(_scroll.customStyle, MarginLeftBaseProperty, out var leftBase)) return;
            if (!UssCustomStyleReader.TryReadPixels(_scroll.customStyle, MarginTopBaseProperty, out var topBase)) return;

            // クリップ領域だけ逃げぶん外へ広げ、グリッドとつまみの位置は動かさない
            // Widen only the clipping region by the clearance, keeping the grid and the thumb in place
            _scroll.style.marginLeft = leftBase - inset;
            _scroll.style.marginTop = topBase - inset;
            _scroll.style.marginBottom = -inset;

            // Web は視口の padding だが、UITK は視口の padding をスクロール範囲に入れないので中身の側に置く（D-47）
            // The Web pads the viewport, but UITK leaves viewport padding out of the scroll range, so pad the content (D-47)
            _scroll.contentContainer.style.paddingTop = inset;
            _scroll.contentContainer.style.paddingBottom = inset;
            _scroll.contentContainer.style.paddingLeft = inset;
        }
    }
}
