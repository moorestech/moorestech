using System;
using System.Linq;
using Core.Master;
using Core.Update;
using Game.Context;
using Game.Map;
using Game.PlayerInventory.Interface;
using MessagePack;
using Microsoft.Extensions.DependencyInjection;
using Mooresmaster.Model.MapModule;
using NUnit.Framework;
using Server.Boot;
using Server.Protocol;
using Server.Protocol.PacketResponse;
using Tests.Module.TestMod;
using Tests.Util;
using UnityEngine;

namespace Tests.CombinedTest.Server.PacketTest
{
    /// <summary>
    ///     vein採掘権威を検証
    ///     Verify vein mining authority
    /// </summary>
    public abstract class VeinMiningProtocolTestBase
    {
        protected const int PlayerId = 1;

        // IronVein内の対象座標
        // Target position inside IronVein
        protected static readonly Vector3Int InsideIronVein = new(0, 5, 0);
        protected static readonly Vector3Int OutsideAnyVein = new(500, 500, 500);
        protected static readonly Vector3Int InsideFluidVein = new(5, 0, 0);
        protected static readonly Vector3Int InsideNoneItemVein = new(20, 5, 0);
        protected static readonly Guid IronVeinGuid = Guid.Parse("11111111-0000-0000-0000-000000000001");
        protected static readonly Guid FluidVeinGuid = Guid.Parse("11111111-0000-0000-0000-000000000002");
        protected static readonly Guid NoneItemVeinGuid = Guid.Parse("11111111-0000-0000-0000-000000000004");
        protected static readonly Guid ToolItemGuid = Guid.Parse("00000000-0000-0000-1234-000000000001");
        protected static readonly Guid UnmatchedToolItemGuid = Guid.Parse("00000000-0000-0000-1234-000000000004");
        protected static readonly Guid MiningMapObjectGuid = Guid.Parse("00000000-0000-2222-0000-000000000001");
        protected const double ExpectedAttackSpeed = 0.2;

        protected void EquipTool(PlayerInventoryData playerInventory, Guid toolItemGuid)
        {
            var toolItemId = MasterHolder.ItemMaster.GetItemId(toolItemGuid);
            playerInventory.EquipmentInventory.SetItem(0, toolItemId, 1);
            playerInventory.EquipmentInventory.SetSelectedEquipmentIndex(0);
        }
    }
}
