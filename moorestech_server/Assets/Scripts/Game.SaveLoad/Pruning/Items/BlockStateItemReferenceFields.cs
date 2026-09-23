using System.Collections.Generic;

namespace Game.SaveLoad.Pruning.Items
{
    /// <summary>ブロックのstateと貨車コンテナの中で、在庫スタック以外の形でアイテムを指す綴り</summary>
    /// <summary>Spellings inside block state and train car containers that point at items in a shape other than an inventory stack</summary>
    /// <summary>コンポーネントごとに形が違うため、在庫スタックの形は全件拾い、それ以外はここに挙げた綴りだけを見る</summary>
    /// <summary>Shapes differ per component, so every inventory stack is caught while other shapes are matched only by the spellings listed here</summary>
    public static class BlockStateItemReferenceFields
    {
        // 裸のguid文字列でアイテムを指す項目。欠損のまま残すとロード時にItemMaster.GetItemIdが例外を投げる
        // Fields pointing at an item with a bare guid string; left missing, ItemMaster.GetItemId throws at load
        public static readonly IReadOnlyList<string> BareItemGuidPropertyNames = new[]
        {
            // VanillaElectricGeneratorSaveJsonObject の [JsonProperty("currentFuelItemGuid")]
            // From VanillaElectricGeneratorSaveJsonObject's [JsonProperty("currentFuelItemGuid")]
            "currentFuelItemGuid",
            // FuelGearGeneratorSaveData はJsonProperty無しでフィールド名がそのまま綴られる
            // FuelGearGeneratorSaveData carries no JsonProperty, so the field name itself is the spelling
            "CurrentFuelItemGuidStr",
        };

        // 接続コスト素材（ElectricWire/GearChainPoleのmaterials）。形は在庫スタックと同じだが在庫ではない
        // Connection cost materials (materials of ElectricWire/GearChainPole); stack-shaped but not inventory
        public const string ConnectionCostMaterialsPropertyName = "materials";

    }
}
