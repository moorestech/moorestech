using System;
using System.Collections.Generic;
using System.Linq;
using Core.Master;
using Core.Inventory;
using Game.Block.Blocks.GearChainPole;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Block.Interface.Extension;
using Game.Context;
using Game.Gear.Common;
using Game.PlayerInventory.Interface;
using Game.UnlockState;
using Game.World.Interface.DataStore;
using MessagePack;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Server.Boot;
using Server.Protocol;
using Server.Protocol.PacketResponse.Util.GearChain;
using Tests.Module;
using Tests.Module.TestMod;
using UnityEngine;
using static Server.Protocol.PacketResponse.RemoveBlockProtocol;

namespace Tests.UnitTest.Game.Chain
{
    public abstract class GearChainSystemUtilTestBase
    {
        protected ServiceProvider _serviceProvider;
        protected PacketResponseCreator _packet;
        protected ItemId _chainItemId;
        protected const int PlayerId = 1;
        protected static readonly Guid ConnectToolGuid = Guid.Parse("c0000000-0000-0000-0000-000000000003");
        protected static readonly Guid ChainMaterialGuid = Guid.Parse("00000000-0000-0000-1234-000000000004");

        [SetUp]
        public void SetUp()
        {
            // テスト用の依存関係を初期化する
            // Initialize dependencies for tests
            var (packet, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            _serviceProvider = serviceProvider;
            _packet = packet;
            _chainItemId = MasterHolder.ItemMaster.GetItemId(ChainMaterialGuid);
            serviceProvider.GetService<IGameUnlockStateDataController>().UnlockConnectTool(ConnectToolGuid);
        }

        protected static int CountItem(IOpenableInventory inventory, ItemId itemId)
        {
            // 対象アイテムの合計数を数える
            // Count total amount of specified item
            var total = 0;
            foreach (var itemStack in inventory.InventoryItems)
            {
                if (itemStack.Id != itemId) continue;
                total += itemStack.Count;
            }

            return total;
        }

        protected static byte[] CreateRemoveBlockPacket(Vector3Int pos)
        {
            return MessagePackSerializer.Serialize(new RemoveBlockProtocolMessagePack(pos));
        }
    }
}
