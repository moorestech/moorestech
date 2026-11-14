using System.Collections.Generic;
using Client.Ui.Components.Controls;
using Client.Ui.Components.Panels;
using Client.Ui.Components.Slots;
using Client.Ui.Content;
using Client.Ui.Foundation.Layout;
using Client.Ui.Foundation.Theme;
using Client.Ui.Screens.Inventory.Parts;
using Client.Ui.State.Inventory.Actions;
using Client.Ui.State.Inventory.Panel;
using Client.Ui.State.Items;
using Client.Ui.State.Text;
using Client.Ui.State.Toast;
using Mooresmaster.Localization.Generated;
using UniRx;
using UnityEngine;
using UnityEngine.UIElements;

namespace Client.Ui.Screens.Inventory.Panel
{
    /// <summary>
    /// 持ち物パネル。操作は枠コマンドと整理コマンドへ
    /// Inventory panel; input goes to slot and sort commands
    /// </summary>
    public sealed class InventoryView : VisualElement
    {
        // :root のトークンはグリッドのクラスで受け直して読む（UssCustomStyleReader は継承値を読まない）
        // The :root token is re-declared on the grid class and read there (UssCustomStyleReader does not read inherited values)
        private const string ColumnsProperty = "--inventory-view-columns";
        private readonly PlayerSlotGestureRouter _gestures;
        private readonly GameContentPresentation _content;
        private readonly SlotGrid _grid;
        private readonly List<ItemSlot> _slots = new();
        private readonly InventorySlotAnchorMarks _anchors = new();
        private int _columns;
        private bool _columnsResolved;

        public InventoryView(PlayerSlotGestureRouter gestures, IInventorySortCommands sort, GameContentPresentation content, UiCommandResultToaster toaster)
        {
            _gestures = gestures;
            _content = content;
            AddToClassList("inventory-view");

            // 題と整理ボタン
            // Title and sort button
            var panel = new GamePanel("inventory-panel", GamePanelFrame.Default);
            panel.AddToClassList("inventory-view__panel");
            panel.SetTitle(new UiText(LocalizationKeys.Ui.Inventory.Title));
            var sortButton = new PanelActionButton("inventory-sort", new UiText(LocalizationKeys.Ui.Inventory.Sort));
            sortButton.OnClicked.Subscribe(_ => toaster.Report(InventorySortCommands.CommandName, sort.Sort()));
            panel.SetTitleAction(sortButton);

            _grid = new SlotGrid("main-grid", 1);
            _grid.AddToClassList("inventory-view__grid");
            _grid.RegisterCallback<CustomStyleResolvedEvent>(_ => ReadColumns());
            panel.Add(_grid);
            Add(panel);
            SetShown(false);
        }

        public void Render(PlayerInventoryViewModel viewModel)
        {
            // 購読は Presenter。ここは描くだけ
            // The presenter subscribes; this only draws
            if (_slots.Count != viewModel.MainSlots.Count) Rebuild(viewModel.MainSlots.Count);
            for (var i = 0; i < _slots.Count; i++) _slots[i].SetContent(SlotItemContent.From(_content, viewModel.MainSlots[i], false));
            _anchors.Apply(_slots, viewModel.MainSlotItemGuids);

            #region Internal

            void Rebuild(int count)
            {
                _grid.ClearItems();
                _slots.Clear();
                _anchors.Clear();
                for (var i = 0; i < count; i++)
                {
                    var index = i;
                    var slot = new ItemSlot($"main-slot-{index}");
                    slot.OnSlotGestured.Subscribe(gesture => _gestures.Route(PlayerSlotRef.Main(index), gesture));
                    _grid.AddItem(slot);
                    _slots.Add(slot);
                }
                ApplyColumns();
            }

            #endregion
        }

        public void SetShown(bool shown)
        {
            UiDisplay.SetShown(this, shown);
        }

        private void ReadColumns()
        {
            // silent-ok: 読めたときだけ解決済みにし、読めなければ列数を当てない / 読めない欠けは UssCustomStyleReader が LogError で開発者へ出す
            if (!UssCustomStyleReader.TryReadNumber(_grid.customStyle, ColumnsProperty, out var columns)) return;
            _columns = (int)columns;
            _columnsResolved = true;
            ApplyColumns();
        }

        private void ApplyColumns()
        {
            // silent-ok: CustomStyleResolvedEvent の前の Rebuild では列数を決めない（解決の時に ReadColumns が当て直す） / 解決しないまま残る欠けは UssCustomStyleReader の LogError で開発者が気づく
            if (!_columnsResolved) return;

            // 解決済みで 1 未満はトークンの誤記なので当てずに出す（fail-closed。グリッドは ctor の 1 列のまま）
            // A resolved value below 1 is a token typo; refuse to apply and report it (fail-closed; the grid keeps the ctor's single column)
            if (_columns < 1)
            {
                Debug.LogError($"[InventoryView] {ColumnsProperty} resolved to {_columns}; columns must be at least 1, so the column count is not applied");
                return;
            }
            _grid.SetColumns(_columns);
        }
    }
}
