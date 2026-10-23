using System.Collections.Generic;
using Core.Item.Interface;
using Newtonsoft.Json;

namespace Game.Block.Blocks.RocketLaunchPad.Save
{
    // 入れ口はアイテムで、出力口は並びで保存する。積荷表が変わってもスロットの意味がずれないようにするため
    // Inputs are saved by item and outputs by order, so slot meaning survives a payload table change
    public class RocketLaunchPadSaveJsonObject
    {
        [JsonProperty("phase")] public int? Phase;
        [JsonProperty("remainingSeconds")] public double RemainingSeconds;
        [JsonProperty("cargoPayload")] public ItemStackSaveJsonObject CargoPayload;
        [JsonProperty("cargoMaterials")] public List<ItemStackSaveJsonObject> CargoMaterials;
        [JsonProperty("nextPayloadIndex")] public int? NextPayloadIndex;
        [JsonProperty("inputItems")] public List<ItemStackSaveJsonObject> InputItems;
        [JsonProperty("outputItems")] public List<ItemStackSaveJsonObject> OutputItems;
    }
}
