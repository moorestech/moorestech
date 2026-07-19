using System.Collections.Generic;
using Client.Game.InGame.UI.Inventory.Main;
using Core.Master;
using Mooresmaster.Model.BlocksModule;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.Common.ElectricWireAutoConnect
{
    /// <summary>
    /// ドラッグ設置プレビュー用の仮想在庫。サーバーの逐次消費（建設コスト予約＋複数素材の電線消費）を再現する
    /// Virtual inventory for drag placement preview, replaying the server's sequential consumption (construction reservation + multi-material wire cost)
    /// </summary>
    public class ElectricWireAutoConnectVirtualInventory
    {
        private readonly Dictionary<ItemId, int> _counts = new();
        private readonly Dictionary<ItemId, int> _constructionCostPerCell = new();

        public ElectricWireAutoConnectVirtualInventory(ILocalPlayerInventory inventory, ConstructionRequiredItemElement[] requiredItems)
        {
            // 所持アイテムをID別に合算する
            // Sum held items per item id
            foreach (var itemStack in inventory)
            {
                if (itemStack.Count <= 0) continue;
                _counts[itemStack.Id] = _counts.GetValueOrDefault(itemStack.Id) + itemStack.Count;
            }

            // セル1つ分の建設コストをID別に合算する
            // Sum one cell's construction cost per item id
            foreach (var requiredItem in requiredItems)
            {
                var itemId = MasterHolder.ItemMaster.GetItemId(requiredItem.ItemGuid);
                _constructionCostPerCell[itemId] = _constructionCostPerCell.GetValueOrDefault(itemId) + requiredItem.Count;
            }
        }

        // サーバー同様、当該セルの建設コスト予約分を上乗せして各素材の所持数を判定する
        // Like the server, judge each material's count with this cell's construction reservation added on top
        public bool CanAfford(IReadOnlyList<ConnectToolMaterialCost> materials)
        {
            if (materials == null) return true;
            foreach (var material in materials)
            {
                var requiredCount = material.Count + _constructionCostPerCell.GetValueOrDefault(material.ItemId);
                if (_counts.GetValueOrDefault(material.ItemId) < requiredCount) return false;
            }
            return true;
        }

        // 設置確定セル分の電線素材と建設コストを仮想在庫から消費する
        // Consume the placed cell's wire materials and construction cost from the virtual inventory
        public void ConsumePlacedCell(IReadOnlyList<ConnectToolMaterialCost> materials)
        {
            if (materials != null)
            {
                foreach (var material in materials)
                {
                    if (material.ItemId == ItemMaster.EmptyItemId || material.Count <= 0) continue;
                    _counts[material.ItemId] = _counts.GetValueOrDefault(material.ItemId) - material.Count;
                }
            }

            foreach (var (itemId, count) in _constructionCostPerCell)
            {
                _counts[itemId] = _counts.GetValueOrDefault(itemId) - count;
            }
        }
    }
}
