using System.Collections.Generic;
using Client.Ui.Components.Controls;
using Client.Ui.Components.Slots;
using Client.Ui.Content;
using Client.Ui.Foundation.Layout;
using Client.Ui.Foundation.Text;
using Client.Ui.Screens.BlockInventory.Special.Registration;
using Client.Ui.State.BlockInventory.Logic.Special;
using Client.Ui.State.BlockInventory.Special.Details;
using Client.Ui.State.BlockInventory.Special.Text;
using Client.Ui.State.Text;
using UniRx;
using UnityEngine.UIElements;

namespace Client.Ui.Screens.BlockInventory.Special.FilterSplitter
{
    /// <summary>
    /// フィルタ分配器の方向 1 つ: 「出力 n」・横の択一（既定/ホワイト/ブラック）・フィルタ枠（左押下で設定、右押下で解除）
    /// One filter splitter direction: "Output n", horizontal exclusive switch (default/white/black), filter slots (left-down sets, right-down clears)
    /// </summary>
    public sealed class FilterDirectionView : VisualElement
    {
        public const string SetModeCommandName = "filter_splitter.set_mode";
        private const string SetItemCommandName = "filter_splitter.set_filter_item";
        private static readonly FilterSplitterMode[] Modes = { FilterSplitterMode.Default, FilterSplitterMode.Whitelist, FilterSplitterMode.Blacklist };

        private readonly SpecialViewKit _kit;
        private readonly int _directionIndex;
        private readonly ModeSwitch<FilterSplitterMode> _mode;
        private readonly UiGroup _slots = new(UiGap.Px4);
        private readonly List<ItemSlot> _slotViews = new();

        public FilterDirectionView(SpecialViewKit kit, int directionIndex)
        {
            _kit = kit;
            _directionIndex = directionIndex;
            name = $"filter-direction-{directionIndex}";
            var stack = new UiStack(UiGap.Xs);
            var output = SpecialViewParts.Label(null, UiTypographyClass.TextSm, UiTypographyClass.TextDefault);
            output.SetText(SpecialDetailTexts.OutputNumber(directionIndex));

            // 択一の選択肢は Web の filterModes の順。押下は目標のモードを明示して送る
            // Options follow the Web filterModes order; a press sends the target mode explicitly
            var options = new List<ModeSwitchOption<FilterSplitterMode>>();
            foreach (var mode in Modes) options.Add(new ModeSwitchOption<FilterSplitterMode>($"filter-mode-{directionIndex}-{mode}", mode, new UiText(FilterSplitterLogic.ModeKey(mode))));
            _mode = new ModeSwitch<FilterSplitterMode>($"filter-mode-{directionIndex}", ModeSwitchOrientation.Horizontal, ModeSwitchRole.Group, options, kit.OsCursor);

            _mode.OnOptionSelected.Subscribe(mode => _kit.Run(SetModeCommandName, _kit.Commands.SetFilterMode(_directionIndex, mode)));

            stack.AddItem(output);
            stack.AddItem(_mode);
            stack.AddItem(_slots);
            Add(stack);
        }

        public void Render(FilterSplitterDirectionDetail direction)
        {
            _mode.SetSelected(direction.Mode);
            EnsureSlotCount(direction.FilterItemIds.Count);
            for (var i = 0; i < _slotViews.Count; i++) _slotViews[i].SetContent(SlotItemContent.ForOptionalItem(_kit.Presentation, direction.FilterItemIds[i]));

            #region Internal

            void EnsureSlotCount(int count)
            {
                // 枠の数はマスタで決まり開いている間は変わらない。違えば作り直す
                // The slot count comes from the master and stays while open; rebuild when it differs
                if (_slotViews.Count == count) return;
                _slots.ClearItems();
                _slotViews.Clear();
                for (var slotIndex = 0; slotIndex < count; slotIndex++)
                {
                    var slot = new ItemSlot($"filter-slot-{_directionIndex}-{slotIndex}");
                    var captured = slotIndex;
                    slot.OnSlotGestured.Subscribe(gesture => OnGesture(captured, gesture.Kind));
                    _slotViews.Add(slot);
                    _slots.AddItem(slot);
                }
            }

            void OnGesture(int slotIndex, SlotGestureKind kind)
            {
                // 左押下は設定、右押下は解除。掴みが空の設定は操作の側が良性に断る（Web の noop）
                // Left-down sets and right-down clears; an empty-grab set is benignly refused by the command (Web noop)
                // silent-ok: 押下以外の身振り（ホバー・なぞり・ダブルクリック）はフィルタ枠の操作ではない / Web の ItemSlot も onLeftDown・onRightDown だけを渡す
                if (kind != SlotGestureKind.LeftDown && kind != SlotGestureKind.RightDown) return;
                _kit.Run(SetItemCommandName, _kit.Commands.SetFilterItem(_directionIndex, slotIndex, kind == SlotGestureKind.RightDown));
            }

            #endregion
        }
    }
}
