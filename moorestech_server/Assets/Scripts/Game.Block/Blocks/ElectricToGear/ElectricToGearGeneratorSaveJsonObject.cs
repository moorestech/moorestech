using Newtonsoft.Json;

namespace Game.Block.Blocks.ElectricToGear
{
    // セーブ対象はランタイム状態のみ。バッテリー容量はマスターデータから再構築する
    // Only runtime state is persisted; the battery capacity is rebuilt from master data
    public class ElectricToGearGeneratorSaveJsonObject
    {
        [JsonProperty("selectedIndex")]
        public int SelectedIndex;

        [JsonProperty("batteryRemaining")]
        public float BatteryRemaining;
    }
}
