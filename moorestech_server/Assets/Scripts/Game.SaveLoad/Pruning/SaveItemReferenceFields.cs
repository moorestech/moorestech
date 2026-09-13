using System.Collections.Generic;

namespace Game.SaveLoad.Pruning
{
    /// <summary>セーブJSONがアイテムを参照する綴りの一覧。対象と除外をここ1箇所だけで決める</summary>
    /// <summary>The single list of spellings a save JSON uses to reference an item, deciding both targets and exclusions</summary>
    /// <summary>新しいアイテム保存形を足したら、その綴りをここへ追加すること（ここに無い形は除去されない）</summary>
    /// <summary>When a new item save shape is added, add its spelling here; a shape absent from this list is never pruned</summary>
    public static class SaveItemReferenceFields
    {
        // 在庫スタックの形。プレイヤー・チェスト・機械の中身がすべてこの形で保存される
        // The inventory stack shape; player, chest and machine contents are all saved like this
        public const string ItemStackItemGuidKey = "itemGuid";
        public const string ItemStackCountKey = "count";

        // 裸のguid文字列でアイテムを指す項目。JObjectの形にならないので上の判定には掛からない
        // Fields that point at an item with a bare guid string; they never take the object shape judged above
        // 欠損のまま残すとロード時にItemMaster.GetItemIdが例外を投げ、ワールドが永久に起動不能になる
        // Left missing, ItemMaster.GetItemId throws at load and the world becomes permanently unloadable
        public static readonly IReadOnlyList<string> BareItemGuidPropertyNames = new[]
        {
            // VanillaElectricGeneratorSaveJsonObject の [JsonProperty("currentFuelItemGuid")]
            // From VanillaElectricGeneratorSaveJsonObject's [JsonProperty("currentFuelItemGuid")]
            "currentFuelItemGuid",
            // FuelGearGeneratorSaveData はJsonProperty無しでフィールド名がそのまま綴られる
            // FuelGearGeneratorSaveData carries no JsonProperty, so the field name itself is the spelling
            "CurrentFuelItemGuidStr",
        };

        // 接続コスト素材。形は在庫スタックと同じだが在庫ではないので、件数と除去データを分ける
        // Connection cost materials; the shape matches an inventory stack but it is not inventory, so counts and archives are kept apart
        public const string ConnectionCostMaterialsPropertyName = "materials";

        // 対象外: filterItemGuids（VanillaFilterSplitter）・miningItemGuids（VanillaMinerProcessor）
        // Out of scope: filterItemGuids (VanillaFilterSplitter) and miningItemGuids (VanillaMinerProcessor)
        // 前者はGetItemIdOrNull、後者は文字列比較だけで解決するためロードを止めず、除去は挙動を変えるだけになる
        // The former resolves through GetItemIdOrNull and the latter only compares strings, so neither blocks a load and pruning them would only change behaviour
    }
}
