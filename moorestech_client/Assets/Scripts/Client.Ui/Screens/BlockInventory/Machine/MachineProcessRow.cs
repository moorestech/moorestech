using Client.Ui.Components.Gauges;
using Client.Ui.Content;
using Client.Ui.Screens.BlockInventory.Machine.Slots;
using Client.Ui.State.BlockInventory.Machine;
using Client.Ui.State.SlotCommands.Block;
using UnityEngine.UIElements;

namespace Client.Ui.Screens.BlockInventory.Machine
{
    /// <summary>
    /// 加工行（Web の processRow）
    /// Process row (Web processRow): input band → progress arrow → output band in three columns, arrow centred
    /// </summary>
    public sealed class MachineProcessRow : VisualElement
    {
        private readonly MachineSlotBand _input;
        private readonly MachineSlotBand _output;
        private readonly ProgressArrowGlyph _arrow = new("machine-progress-arrow");

        public MachineProcessRow(GameContentPresentation content, IBlockSlotCommands commands)
        {
            name = "machine-process-row";
            pickingMode = PickingMode.Ignore;
            AddToClassList("machine-process-row");
            _input = new MachineSlotBand("machine-input-slots", "machine-input-slot", commands, content);
            _output = new MachineSlotBand("machine-output-slots", "machine-output-slot", commands, content);

            // 左右を等幅で伸ばし矢印を中央に固定
            // Stretch both side boxes equally to pin the arrow at the centre (Web grid 1fr auto 1fr)
            var inputSide = Side("machine-process-row__input", _input);
            var outputSide = Side("machine-process-row__output", _output);
            Add(inputSide);
            Add(_arrow);
            Add(outputSide);

            // Web の 1fr は minmax(auto, 1fr): 列は自分の中身より狭くならず、収まる面では両列が等幅（中身で決まる面では広い方の中身の幅）。収まらない面では広い側が中身の幅に留まり狭い側が残りを取る
            // Web 1fr is minmax(auto, 1fr): a column never shrinks below its own content, both columns are equal when they fit (the wider content in a content-sized panel), and when they do not fit the wider side stays content-wide while the narrower takes the rest
            _input.Grid.RegisterCallback<GeometryChangedEvent>(_ => MatchSideWidths());
            _output.Grid.RegisterCallback<GeometryChangedEvent>(_ => MatchSideWidths());

            #region Internal

            void MatchSideWidths()
            {
                // 基準の幅を両側とも広い方の中身にして等幅で伸び縮みさせ、下限は各自の中身にする（Yoga は下限に当たった側を止めて残りを他方へ回す）
                // Both sides take the wider content as their basis so they grow and shrink equally, floored at their own content (Yoga freezes a side at its floor and gives the rest to the other)
                var inputWidth = _input.Grid.layout.width;
                var outputWidth = _output.Grid.layout.width;
                var widest = System.Math.Max(inputWidth, outputWidth);
                SetSideWidth(inputSide, inputWidth, widest);
                SetSideWidth(outputSide, outputWidth, widest);
            }

            static void SetSideWidth(VisualElement side, float content, float basis)
            {
                // 同じ値の付け直しは配置を回し続けるので当てない
                // Skip re-applying equal values, which would keep the layout looping
                if (UnityEngine.Mathf.Approximately(side.resolvedStyle.minWidth.value, content) && UnityEngine.Mathf.Approximately(side.resolvedStyle.flexBasis.value, basis)) return;
                side.style.minWidth = content;
                side.style.flexBasis = basis;
            }

            static VisualElement Side(string className, MachineSlotBand band)
            {
                var side = new VisualElement { pickingMode = PickingMode.Ignore };
                side.AddToClassList(className);
                side.Add(band.Grid);
                return side;
            }

            #endregion
        }

        public void Render(MachineSectionViewModel viewModel)
        {
            // 素材の枠だけ所持/必要を出す
            // Only material slots show held/required; outputs never do, even when filled
            _input.Render(viewModel.SlotView.Inputs, viewModel.ItemSlots, true);
            _output.Render(viewModel.SlotView.Outputs, viewModel.ItemSlots, false);
            _arrow.SetProgress(viewModel.Detail.Progress);
        }
    }
}
