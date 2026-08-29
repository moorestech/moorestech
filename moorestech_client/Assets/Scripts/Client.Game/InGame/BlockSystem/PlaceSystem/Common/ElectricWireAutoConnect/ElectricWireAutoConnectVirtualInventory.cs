using System.Collections.Generic;
using Client.Game.InGame.BlockSystem.PlaceSystem.Util;
using Client.Game.InGame.UI.Inventory.Main;
using Core.Master;
using Server.Protocol.PacketResponse.Util.ConnectTool;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.Common.ElectricWireAutoConnect
{
    /// <summary>
    /// ドラッグ設置プレビュー用の仮想在庫。サーバーの逐次消費（建設コスト予約＋複数素材の電線消費）を再現する
    /// Virtual inventory for drag placement preview, replaying the server's sequential consumption (construction reservation + multi-material wire cost)
    /// </summary>
    public class ElectricWireAutoConnectVirtualInventory
    {
        private readonly Dictionary<ItemId, int> _counts = new();

        // 予約分は不足算出へそのまま渡せるようサーバー標準の素材コスト列で持つ
        // The reservation is kept as the server-standard material cost list so it can be handed to the shortage calculation as is
        private readonly IReadOnlyList<ConnectToolMaterialCost> _constructionCostPerCell;

        public ElectricWireAutoConnectVirtualInventory(ILocalPlayerInventory inventory, IReadOnlyList<(ItemId itemId, int count)> constructionCostPerCell)
        {
            // 所持アイテムをID別に合算する
            // Sum held items per item id
            foreach (var itemStack in inventory)
            {
                if (itemStack.Count <= 0) continue;
                _counts[itemStack.Id] = _counts.GetValueOrDefault(itemStack.Id) + itemStack.Count;
            }

            // セル1つ分の建設コストを予約素材へ変換する。財布が賄うセルでは空で渡される
            // Convert one cell's construction cost into reserved materials; a wallet-covered cell arrives empty
            _constructionCostPerCell = ConnectToolMaterialConsumer.ToMaterials(constructionCostPerCell);
        }

        // サーバー同様、当該セルの建設コスト予約分を上乗せして各素材の所持数を判定する
        // Like the server, judge each material's count with this cell's construction reservation added on top
        public bool CanAfford(IReadOnlyList<ConnectToolMaterialCost> materials)
        {
            return CalculateShortages(materials).Count == 0;
        }

        // 賄えない素材を「所持/必要」付きで返す。可否判定はこの結果が空かどうかで決まる
        // Returns the unaffordable materials with held/required; affordability is defined as this result being empty
        public List<ConstructionMaterialShortage> CalculateShortages(IReadOnlyList<ConnectToolMaterialCost> materials)
        {
            return ConnectToolMaterialShortageCalculator.Calculate(materials, _counts, _constructionCostPerCell);
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

            foreach (var reserved in _constructionCostPerCell)
            {
                _counts[reserved.ItemId] = _counts.GetValueOrDefault(reserved.ItemId) - reserved.Count;
            }
        }
    }
}
