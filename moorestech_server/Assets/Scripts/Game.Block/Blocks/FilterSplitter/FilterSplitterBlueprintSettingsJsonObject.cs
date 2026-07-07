using System.Collections.Generic;
using Newtonsoft.Json;

namespace Game.Block.Blocks.FilterSplitter
{
    /// <summary>
    ///     BPへコピーされるフィルタスプリッタ設定のJSON形
    ///     JSON shape of filter splitter settings copied into blueprints
    /// </summary>
    public class FilterSplitterBlueprintSettingsJsonObject
    {
        [JsonProperty("directions")] public List<FilterSplitterBlueprintDirectionSettingJsonObject> Directions;
    }

    public class FilterSplitterBlueprintDirectionSettingJsonObject
    {
        [JsonProperty("connectorGuid")] public string ConnectorGuid;
        [JsonProperty("mode")] public int Mode;

        // 空スロットはnull（セーブデータのfilterItemGuidsと同じ慣例）
        // Empty slots are null, matching the save data's filterItemGuids convention
        [JsonProperty("filterItemGuids")] public List<string> FilterItemGuids;
    }
}
