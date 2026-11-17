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

            // Web の 1fr は minmax(auto, 1fr) で、列は中身より狭くならず内容で決まる面では両列が広い方の中身の幅に揃う。両側の min-width を帯の広い方の幅にして写す
            // Web 1fr is minmax(auto, 1fr): a column never shrinks below its content, and in a content-sized panel both columns match the wider content; port it as both sides' min-width set to the wider band
            _input.Grid.RegisterCallback<GeometryChangedEvent>(_ => MatchSideMinWidth());
            _output.Grid.RegisterCallback<GeometryChangedEvent>(_ => MatchSideMinWidth());

            #region Internal

            void MatchSideMinWidth()
            {
                var widest = System.Math.Max(_input.Grid.layout.width, _output.Grid.layout.width);
                if (UnityEngine.Mathf.Approximately(inputSide.resolvedStyle.minWidth.value, widest)) return;
                inputSide.style.minWidth = widest;
                outputSide.style.minWidth = widest;
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
