using System;
using System.Collections.Generic;
using Core.Master;
using UnityEngine;

namespace Server.Protocol.PacketResponse.Util.ConnectTool
{
    /// <summary>
    /// connectToolマスタと接続距離から複数素材の消費量を算出する共有ロジック。
    /// units = ceil(距離 / lengthPerUnit)、各requiredItemの消費 = units × count。
    /// Shared logic computing multi-material consumption from the connectTool master and connection distance.
    /// units = ceil(distance / lengthPerUnit); each requiredItem consumes units × count.
    /// </summary>
    public static class ConnectToolCostCalculator
    {
        public static bool TryCalculate(Guid connectToolGuid, float distance, out IReadOnlyList<ConnectToolMaterialCost> materials)
        {
            materials = null;

            // マスタに存在しないconnectToolは算出不能
            // Cannot compute for a connectTool absent from the master
            var element = MasterHolder.ConnectToolMaster.GetElementOrNull(connectToolGuid);
            if (element == null) return false;

            // 距離を1単位長で割り上げた単位数に各素材のcountを掛ける
            // Multiply each material count by the unit count = ceil(distance / lengthPerUnit)
            var units = Mathf.CeilToInt(distance / element.LengthPerUnit);
            var list = new List<ConnectToolMaterialCost>(element.RequiredItems.Length);
            foreach (var requiredItem in element.RequiredItems)
            {
                var itemId = MasterHolder.ItemMaster.GetItemId(requiredItem.ItemGuid);
                list.Add(new ConnectToolMaterialCost(itemId, units * requiredItem.Count));
            }

            materials = list;
            return true;
        }
    }
}
