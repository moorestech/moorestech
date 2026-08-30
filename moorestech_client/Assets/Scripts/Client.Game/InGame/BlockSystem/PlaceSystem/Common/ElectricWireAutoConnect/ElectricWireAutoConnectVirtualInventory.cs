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

        // 予約分はサーバー標準の素材コスト列で持つ
        // The reservation is kept as the server-standard material cost list
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

            // 建設コストを予約素材へ変換する
            // Convert one cell's construction cost into reserved materials
            _constructionCostPerCell = ConnectToolMaterialConsumer.ToMaterials(constructionCostPerCell);
        }

        // サーバー同様、当該セルの建設コスト予約分を上乗せして各素材の所持数を判定する
        // Like the server, judge each material's count with this cell's construction reservation added on top
        // 可否だけが要るのでリストは作らない。必要数の式は不足算出と共有する
        // Only affordability is needed here so no list is built; the requirement formula is shared with the shortage calculation
        public bool CanAfford(IReadOnlyList<ConnectToolMaterialCost> materials)
        {
            return !ConnectToolMaterialShortageCalculator.HasAnyShortage(materials, _counts, _constructionCostPerCell);
        }

        // 賄えない素材を「所持/必要」付きで返す。表示行を出すときだけ呼ぶ
        // Returns the unaffordable materials with held/required; called only when a display line is needed
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
