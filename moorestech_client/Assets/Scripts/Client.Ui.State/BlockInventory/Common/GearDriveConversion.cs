using Client.Game.InGame.Block;
using Game.Gear.Common;
using Mooresmaster.Model.BlocksModule;

namespace Client.Ui.State.BlockInventory.Common
{
    /// <summary>
    /// 歯車駆動か。純関数で、異常はログでなく結果に載せて返す（呼び手の Model が自分の旗で 1 回出す）
    /// Whether gear-driven; port of Web GearDetailDtoBuilder.Apply. Pure: an anomaly is returned in the result and the calling Model reports it once with its own flag
    /// </summary>
    public static class GearDriveConversion
    {
        public static GearDriveReading Read(BlockGameObject block, object param)
        {
            return Read(block.GetStateDetail<GearStateDetail>(GearStateDetail.BlockStateDetailKey), param);
        }

        public static GearDriveReading Read(GearStateDetail gear, object param)
        {
            // 歯車の詳細が無ければ電力駆動
            // No gear state detail means electric, as Web data.gear === undefined
            if (gear == null) return GearDriveReading.Healthy(false);
            if (gear.Role == GearRole.Generator) return GearDriveReading.Healthy(true);

            // 消費の値が無いのは不整合。Web と同じく電力扱いにして異常を返す
            // A consumer without a consumption param is inconsistent; like Web, treat as electric and return the anomaly
            if (param is IGearConsumptionParam) return GearDriveReading.Healthy(true);
            return GearDriveReading.Inconsistent($"[GearDriveConversion] Server reports GearRole.Consumer but block param {param?.GetType().Name} has no IGearConsumptionParam; treated as electric like the Web host");
        }
    }

    public readonly struct GearDriveReading
    {
        public readonly bool IsGearDriven;
        // 異常の文面。無ければ null
        // Anomaly text; null when none
        public readonly string Anomaly;

        private GearDriveReading(bool isGearDriven, string anomaly)
        {
            IsGearDriven = isGearDriven;
            Anomaly = anomaly;
        }

        public static GearDriveReading Healthy(bool isGearDriven) => new(isGearDriven, null);
        public static GearDriveReading Inconsistent(string anomaly) => new(false, anomaly);
    }
}
