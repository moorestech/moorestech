using Newtonsoft.Json;

namespace Game.Block.Blocks.OrbitalCargo
{
    // 選んだ積荷をアイテム GUID で残す。未選択で保存した台は null（項目そのものが無い旧セーブとは別）
    // Persist the selected cargo by item GUID; null for a pad saved unselected (distinct from an old save without the item)
    public class OrbitalCargoSelectionSaveJsonObject
    {
        [JsonProperty("selectedCargoItemGuid")] public string SelectedCargoItemGuid;
    }
}
