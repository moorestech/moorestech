using System;
using Core.Master;
using Newtonsoft.Json;

namespace Game.Block.Interface.Component
{
    /// <summary>
    /// 接続コスト1素材の永続化表現。揮発ItemIdでなくItemGuidで保存し、ロード時に解決する
    /// Persistence form of one cost material; stores ItemGuid (not volatile ItemId) and resolves on load
    /// </summary>
    public class ConnectToolMaterialSaveJsonObject
    {
        [JsonProperty("itemGuid")] public string ItemGuidStr { get; set; }
        [JsonProperty("count")] public int Count { get; set; }

        [JsonIgnore] public Guid ItemGuid => Guid.Parse(ItemGuidStr);

        public ConnectToolMaterialSaveJsonObject() { }

        public ConnectToolMaterialSaveJsonObject(ConnectToolMaterialCost material)
        {
            ItemGuidStr = MasterHolder.ItemMaster.GetItemGuid(material.ItemId).ToString();
            Count = material.Count;
        }

        // ロード時にItemGuidからItemIdを解決してコスト素材へ戻す
        // Resolve ItemId from ItemGuid on load and return the cost material
        public ConnectToolMaterialCost ToMaterialCost()
        {
            return new ConnectToolMaterialCost(MasterHolder.ItemMaster.GetItemId(ItemGuid), Count);
        }
    }
}
