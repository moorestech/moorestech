using System.Collections.Generic;
using Client.Game.InGame.UI.Inventory.Main;
using Core.Master;
using Mooresmaster.Model.BlocksModule;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.Common.ElectricWireAutoConnect
{
    /// <summary>
    /// ドラッグ設置プレビュー用の仮想在庫。サーバーの逐次消費（建設コスト予約＋電線消費）を再現する
    /// Virtual inventory for drag placement preview, replaying the server's sequential consumption (construction reservation + wire cost)
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

        // サーバー同様、当該セルの建設コスト予約分を上乗せして電線所持数を判定する
        // Like the server, judge the wire count with this cell's construction reservation added on top
        public bool CanAffordWire(ItemId wireItemId, int wireCost)
        {
            var requiredCount = wireCost + _constructionCostPerCell.GetValueOrDefault(wireItemId);
            return requiredCount <= _counts.GetValueOrDefault(wireItemId);
        }

        // 設置確定セル分の電線と建設コストを仮想在庫から消費する
        // Consume the placed cell's wire cost and construction cost from the virtual inventory
        public void ConsumePlacedCell(ItemId wireItemId, int wireCost)
        {
            if (wireItemId != ItemMaster.EmptyItemId && 0 < wireCost)
            {
                _counts[wireItemId] = _counts.GetValueOrDefault(wireItemId) - wireCost;
            }

            foreach (var (itemId, count) in _constructionCostPerCell)
            {
                _counts[itemId] = _counts.GetValueOrDefault(itemId) - count;
            }
        }
    }
}
