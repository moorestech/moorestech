using System;
using Client.Ui.Components.Controls;
using Client.Ui.Content;
using Client.Ui.Foundation.Input.OsCursor;
using Client.Ui.Foundation.Layout;
using Client.Ui.Screens.BlockInventory.Context;
using Client.Ui.State.BlockInventory.Context;
using Client.Ui.State.BlockInventory.Machine;
using Client.Ui.State.SlotCommands.Block;
using Client.Ui.State.Toast;
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
        private readonly BlockContextSectionsView _context;
        private readonly SelectedRecipeHeader _header;
        private readonly MachineInventoryBody _body;
        private readonly MachineSectionFooter _footer = new();

        public VisualElement Root => _root;
        public IObservable<Unit> OnChangeRecipeRequested => _header.OnPressed;

        public MachineSectionView(bool fillsPanelHeight, GameContentPresentation content, IBlockSlotCommands commands, UiCommandResultToaster toaster, IUiOsCursor osCursor)
        {
            // 枠の押下は本体の枠が直接コマンドへ渡す（設計書 §4.2 A-8。Presenter は中継しない）
            // Slot presses go straight from the body's slots to the commands (design §4.2 A-8; no presenter relay)
            _root.pickingMode = PickingMode.Ignore;
            _inventoryContent.pickingMode = PickingMode.Ignore;
            _context = new BlockContextSectionsView(content);
            _header = new SelectedRecipeHeader(content, osCursor);
            _body = new MachineInventoryBody(content, commands, toaster);
            _inventoryContent.AddItem(_context);
            _inventoryContent.AddItem(_header);
            _inventoryContent.AddItem(_body);
            _inventoryContent.SetItemShown(_context, false);
            _inventoryContent.SetItemShown(_header, false);

            // 大型面の本文は器高のスクロール器に入れ、フッタに重ならない（列車の本文と同じ土台）
            // On the large panel the body sits in a container-height scroller so it never overlaps the footer
            _root.EnableInClassList(FillPanelHeightClassName, fillsPanelHeight);
            _root.AddItem(fillsPanelHeight ? WrapInScroll(_inventoryContent) : _inventoryContent);
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
            // 選択モードは Task 12 で選択リストを足す。それまで showsSelection は本体を出したままにする
            // Selection mode gets its list in Task 12; until then showsSelection keeps the body shown
            var hasSelectedRow = viewModel.TryGetSelectedRow(out var selectedRow);
            _inventoryContent.SetItemShown(_header, hasSelectedRow);
            if (hasSelectedRow) _header.Render(selectedRow, viewModel);
            _body.Render(viewModel);
            _footer.Render(viewModel.Detail);
        }

        public void RenderContext(BlockContextViewModel context)
        {
            // 文脈の通知では文脈の 3 欄だけを描く（本体は機械の通知だけで描く。D-39）
            // A context update redraws only the three context entries (the body redraws only on machine updates; D-39)
            _context.Render(context);
            _inventoryContent.SetItemShown(_context, context.HasAny);
        }
    }
}
