using System.Collections.Generic;

namespace Client.Ui.State.BlockInventory.Special.Details
{
    // 回転生成機の出力モード 1 つ（マスタの順が添字の意味）
    // One ElectricToGear output mode; master order is the meaning of the index
    public readonly struct ElectricToGearOutputMode
    {
        public double Rpm { get; }
        public double Torque { get; }
        public double RequiredPower { get; }

        public ElectricToGearOutputMode(double rpm, double torque, double requiredPower)
        {
            Rpm = rpm;
            Torque = torque;
            RequiredPower = requiredPower;
        }
    }

    /// <summary>
    /// 回転生成機の詳細（Web ElectricToGearDetailDto と同じ値）
    /// ElectricToGear generator detail (same values as Web ElectricToGearDetailDto)
    /// </summary>
    public sealed class ElectricToGearDetail
    {
        public IReadOnlyList<ElectricToGearOutputMode> OutputModes { get; }
        public int SelectedIndex { get; }
        public double FulfillmentRate { get; }
        public double ConsumedElectricPower { get; }

        public ElectricToGearDetail(IReadOnlyList<ElectricToGearOutputMode> outputModes, int selectedIndex, double fulfillmentRate, double consumedElectricPower)
        {
            OutputModes = outputModes;
            SelectedIndex = selectedIndex;
            FulfillmentRate = fulfillmentRate;
            ConsumedElectricPower = consumedElectricPower;
        }

        // 選択肢の作り直しが要るか（モードの数と値が同じなら選択の差し替えだけで済む）
        // Whether the options must be rebuilt; same count and values only need a selection update
        public bool HasSameModes(ElectricToGearDetail other)
        {
            if (other.OutputModes.Count != OutputModes.Count) return false;
            for (var i = 0; i < OutputModes.Count; i++)
            {
                if (!OutputModes[i].Equals(other.OutputModes[i])) return false;
            }
            return true;
        }
    }
}
