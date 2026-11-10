using System;
using Client.Ui.Components.Controls;
using Client.Ui.Content;
using Client.Ui.Foundation.Input.OsCursor;
using Client.Ui.Foundation.Layout;
using Client.Ui.Screens.BlockInventory.Context;
using Client.Ui.Screens.BlockInventory.Machine.RecipeSelection;
using Client.Ui.State.BlockInventory.Context;
using Client.Ui.State.BlockInventory.Machine;
using Client.Ui.State.BlockInventory.Machine.Selection;
using Client.Ui.State.SlotCommands.Block;
using Client.Ui.State.Toast;
using Cysharp.Threading.Tasks;
using UniRx;
using UnityEngine.UIElements;

namespace Client.Ui.Screens.BlockInventory.Machine
{
    /// <summary>
    /// 機械の区画（Web MachineSection）。外殻がフッタと伸長を 1 か所で持ち、モードで分かれるのは中身だけ
    /// The machine section (Web MachineSection); one shell owns the footer and stretch, only the body branches by mode
    /// </summary>
    public sealed class MachineSectionView
    {
        public const string FillPanelHeightClassName = "machine-section--fill-panel-height";

        private readonly UiStack _root = new(UiGap.Sm) { name = "machine-section" };
        private readonly UiStack _inventoryContent = new(UiGap.Sm);
        private readonly VisualElement _inventoryHost;
        private readonly MachineRecipeSelectionList _selectionList;
        private readonly BlockContextSectionsView _context;
        private readonly SelectedRecipeHeader _header;
        private readonly MachineInventoryBody _body;
        private readonly MachineSectionFooter _footer = new();
        private readonly IMachineRecipeSelectCommand _selectCommand;
        private readonly UiCommandResultToaster _toaster;
        private bool _contextHasAny;
        private bool _showsSelection;

        public VisualElement Root => _root;
        public IObservable<Unit> OnChangeRecipeRequested => _header.OnPressed;
        public IObservable<Guid> OnRecipeRowPressed => _selectionList.OnRowPressed;

        public MachineSectionView(bool fillsPanelHeight, GameContentPresentation content, IBlockSlotCommands commands, UiCommandResultToaster toaster, IUiOsCursor osCursor, IMachineRecipeSelectCommand selectCommand)
        {
            // 枠の押下とレシピ行の押下は View が直接コマンドへ渡す（設計書 §4.2 A-8・D-33。Presenter は中継しない）
            // Slot and recipe-row presses go straight from the view to the commands (design §4.2 A-8, D-33; no presenter relay)
            _selectCommand = selectCommand;
            _toaster = toaster;
            _root.pickingMode = PickingMode.Ignore;
            _inventoryContent.pickingMode = PickingMode.Ignore;
            _context = new BlockContextSectionsView(content);
            _header = new SelectedRecipeHeader(content, osCursor);
            _body = new MachineInventoryBody(content, commands, toaster);
            _selectionList = new MachineRecipeSelectionList(content, osCursor);
            StackInventoryContent();
            _inventoryContent.SetItemShown(_context, false);
            _inventoryContent.SetItemShown(_header, false);
            _selectionList.OnRowPressed.Subscribe(guid => SelectAsync(guid).Forget());

            // 大型面の本文は器高のスクロール器に入れ、フッタに重ならない（列車の本文と同じ土台）
            // On the large panel the body sits in a container-height scroller so it never overlaps the footer
            _root.EnableInClassList(FillPanelHeightClassName, fillsPanelHeight);
            _inventoryHost = fillsPanelHeight ? WrapInScroll(_inventoryContent) : _inventoryContent;
            _root.AddItem(_inventoryHost);
            _root.AddItem(_footer);

            #region Internal

            static VisualElement WrapInScroll(VisualElement inventoryContent)
            {
                var scroll = new GameScrollView("machine-inventory-scroll");
                scroll.AddToClassList("machine-section__inventory-scroll");
                scroll.Add(inventoryContent);
                return scroll;
            }

            #endregion
        }

        public void Render(MachineSectionViewModel viewModel, bool showsSelection)
        {
            // 積み直しと付け替えはモードが変わったときだけ（同じモードの再描画では要素を動かさない。D-37）
            // Restack and re-attach only on a mode change (same-mode redraws never move elements; D-37)
            if (showsSelection != _showsSelection) SwitchMode(showsSelection);
            if (showsSelection)
            {
                _selectionList.Render(viewModel, _context);
            }
            else
            {
                var hasSelectedRow = viewModel.TryGetSelectedRow(out var selectedRow);
                _inventoryContent.SetItemShown(_header, hasSelectedRow);
                if (hasSelectedRow) _header.Render(selectedRow, viewModel);
                _body.Render(viewModel);
            }
            _footer.Render(viewModel.Detail);
        }

        public void RenderContext(BlockContextViewModel context)
        {
            // 文脈の通知では文脈の 3 欄だけを描く（本体は機械の通知だけで描く。D-39）。どちらのモードでも同じ要素の出し入れ
            // A context update redraws only the three context entries (the body redraws only on machine updates; D-39); the same element toggles in either mode
            _context.Render(context);
            _contextHasAny = context.HasAny;
            _inventoryContent.SetItemShown(_context, context.HasAny);
        }

        private void SwitchMode(bool showsSelection)
        {
            // 選択モードでは文脈を選択リストの先頭へ移し、戻るときは本体の器へ文脈 → ヘッダ → 本体の順に積み直す
            // Selection mode moves the context to the list's leading slot; returning restacks context → header → body in the inventory box
            _showsSelection = showsSelection;
            _selectionList.ClearLeadingContent();
            if (!showsSelection)
            {
                StackInventoryContent();
                _inventoryContent.SetItemShown(_context, _contextHasAny);
            }

            // 木に置くのは今のモードの中身だけ（Web も片方だけを描く）。フッタは常に末尾
            // Only the current mode's content is in the tree (the Web renders just one); the footer always comes last
            _root.ClearItems();
            _root.AddItem(showsSelection ? _selectionList : _inventoryHost);
            _root.AddItem(_footer);
        }

        private void StackInventoryContent()
        {
            // UiGapContainer に先頭へ入れる口は無いので、全部外して並びの順に足し直す
            // UiGapContainer has no insert-at-front, so clear and re-add in order
            _inventoryContent.ClearItems();
            _inventoryContent.AddItem(_context);
            _inventoryContent.AddItem(_header);
            _inventoryContent.AddItem(_body);
        }

        private async UniTaskVoid SelectAsync(Guid recipeGuid)
        {
            // 送るか・受理かの判断はコマンド（State）が持つ。拒否は非良性なので Web と同じくトーストする（D-38）
            // The command (State) decides whether to send or accept; rejections are non-benign and toast like the Web (D-38)
            var result = await _selectCommand.Select(recipeGuid);
            _toaster.Report(MachineRecipeSelectCommand.CommandName, result);
        }
    }
}
