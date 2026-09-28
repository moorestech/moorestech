using System;
using System.Linq;
using Core.Inventory;
using Core.Update;
using Core.Master;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Block.Interface.Extension;
using Game.Context;
using Game.EnergySystem;
using Game.PlayerInventory.Interface;
using Game.SaveLoad.Interface;
using Game.SaveLoad.Json;
using Game.UnlockState;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Server.Boot;
using Server.Protocol.PacketResponse.Util.ElectricWire;
using Tests.Module.TestMod;
using UnityEngine;
using static Tests.Util.EnergySystem.ElectricNetworkReflectionTestUtil;
using static Tests.Module.TestMod.ForUnitTestModBlockId;
using Server.Protocol.PacketResponse.Util.ElectricWire.Connection;

namespace Tests.CombinedTest.Game.Helpers
{
    // 電線のセーブロード検証で使う位置と個数の補助
    // Shared position and item-count helpers for wire save-load checks
    internal static class ElectricWireSaveLoadTestHelpers
    {
        internal static Vector3Int Pos(int x, int z)
        {
            return new Vector3Int(x, 0, z);
        }

        internal static int CountItem(IOpenableInventory inventory, ItemId itemId)
        {
            var total = 0;
            foreach (var itemStack in inventory.InventoryItems)
                if (itemStack.Id == itemId)
                    total += itemStack.Count;
            return total;
        }
    }
}
