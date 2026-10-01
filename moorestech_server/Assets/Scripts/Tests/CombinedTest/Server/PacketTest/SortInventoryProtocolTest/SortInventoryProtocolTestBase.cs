using System;
using System.Linq;
using Core.Item;
using Core.Master;
using Game.Block.Blocks.Chest;
using Game.Block.Blocks.Machine.Inventory;
using Game.Block.Interface;
using Game.Block.Interface.Extension;
using Game.Context;
using Game.PlayerInventory.Interface;
using MessagePack;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Server.Boot;
using Server.Protocol;
using Server.Util.MessagePack;
using Tests.Module.TestMod;
using Tests.Util;
using UnityEngine;
using static Server.Protocol.PacketResponse.SortInventoryProtocol;

namespace Tests.CombinedTest.Server.PacketTest
{
    public abstract class SortInventoryProtocolTestBase
    {
        protected const int PlayerId = 1;

        protected byte[] GetPacket(InventoryIdentifierMessagePack target)
        {
            return MessagePackSerializer.Serialize(new SortInventoryProtocolMessagePack(target));
        }
    }
}
