using System;
using System.Collections.Generic;

namespace Client.Ui.State.BlockInventory.Logic.Detail
{
    /// <summary>
    /// 機械のスロット構成（入力・出力・モジュールの数）と、統合 index の帯への分割
    /// A machine's slot layout (input, output and module counts) and the split of combined indices into bands
    /// </summary>
    public readonly struct MachineSlotLayout
    {
        public int Input { get; }
        public int Output { get; }
        public int Module { get; }

        public MachineSlotLayout(int input, int output, int module)
        {
            Input = input;
            Output = output;
            Module = module;
        }

        // 統合 index を 入力→出力→モジュール に割る（uGUI のスロット構成順）。総数を超える添字は作らない
        // Split combined indices into input→output→module (uGUI order); never yields indices past the total
        public MachineSlotBands Split(int totalSlots)
        {
            var all = BlockDetailMath.AllSlotIndices(totalSlots);
            var inputEnd = Input;
            var outputEnd = Input + Output;
            var moduleEnd = Input + Output + Module;
            return new MachineSlotBands(Slice(0, inputEnd), Slice(inputEnd, outputEnd), Slice(outputEnd, moduleEnd));

            #region Internal

            List<int> Slice(int start, int end)
            {
                // Web の Array.slice と同じく範囲外は切り詰める
                // Truncate out-of-range bounds like Web Array.slice
                // silent-ok: 構成と総数の食い違いは Web と同じく欠けた帯として描く（判断記録 D-19） / 欠けた帯は S4-machine の参照キャプチャとの並べ比べと録画で見える
                var from = Math.Min(start, all.Length);
                var to = Math.Min(end, all.Length);
                var band = new List<int>();
                for (var i = from; i < to; i++) band.Add(all[i]);
                return band;
            }

            #endregion
        }
    }

    public sealed class MachineSlotBands
    {
        public IReadOnlyList<int> Input { get; }
        public IReadOnlyList<int> Output { get; }
        public IReadOnlyList<int> Module { get; }

        public MachineSlotBands(IReadOnlyList<int> input, IReadOnlyList<int> output, IReadOnlyList<int> module)
        {
            Input = input;
            Output = output;
            Module = module;
        }
    }
}
