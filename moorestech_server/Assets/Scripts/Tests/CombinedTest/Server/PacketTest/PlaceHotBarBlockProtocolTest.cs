using System.Collections.Generic;
using System.Linq;
using Core.Master;
using Game.Block.Interface;
using Game.Context;
using Game.PlayerInventory.Interface;
using MessagePack;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Server.Boot;
using Server.Protocol.PacketResponse;
using Tests.Module.TestMod;
using UnityEngine;

namespace Tests.CombinedTest.Server.PacketTest
{
    public class PlaceHotBarBlockProtocolTest
    {
        private const int PlacedBlockId = 1;
        private static readonly ItemId BlockItemId = new(1);
        private const int PlayerId = 3;
        private const int HotBarSlot = 3;
        
        [Test]
        public void BlockPlaceTest()
        {
            var (packet, serviceProvider) =
                new MoorestechServerDIContainerGenerator().Create(TestModDirectory.ForUnitTestModDirectory);
            var itemStackFactory = ServerContext.ItemStackFactory;
            
            //パケットでプレイヤーインベントリを生成
            
            //ホットバーにアイテムとしてのブロックをセットする
            var slot = PlayerInventoryConst.HotBarSlotToInventorySlot(HotBarSlot);
            var inventory = serviceProvider.GetService<IPlayerInventoryDataStore>().GetInventoryData(PlayerId);
            inventory.MainOpenableInventory.SetItem(slot, itemStackFactory.Create(BlockItemId, 3));
            
            //ブロックを置く
            packet.GetPacketResponse(CreateUseHotBarProtocol(2, 4, 0));
            
            //ブロックが置かれているかチェック
            var world = ServerContext.WorldBlockDatastore;
            Assert.AreEqual(PlacedBlockId, world.GetBlock(new Vector3Int(2, 4)).BlockId);
            //アイテムが減っているかチェック
            Assert.AreEqual(2, inventory.MainOpenableInventory.GetItem(slot).Count);
            
            
            //既にブロックがあるところに置こうとしてもアイテムが減らないテスト
            packet.GetPacketResponse(CreateUseHotBarProtocol(2, 4, 0));
            //アイテムが減っていないかのチェック
            Assert.AreEqual(2,
                inventory.MainOpenableInventory.GetItem(slot).Count);
            
            //ホットバー内のアイテムを使い切る
            packet.GetPacketResponse(CreateUseHotBarProtocol(3, 4, 0));
            packet.GetPacketResponse(CreateUseHotBarProtocol(4, 4, 0));
            //ホットバーのアイテムが空になっているかのテスト
            Assert.AreEqual(itemStackFactory.CreatEmpty(), inventory.MainOpenableInventory.GetItem(slot));
            
            
            //さらにブロックを置こうとしても置けないテスト
            packet.GetPacketResponse(CreateUseHotBarProtocol(10, 10, 0));
            Assert.True(world.GetBlock(new Vector3Int(10, 10)) == null);
        }
        
        
        //ブロックの設置する向きが正しいかテスト
        [Test]
        public void PlaceDirectionTest()
        {
            var (packet, serviceProvider) =
                new MoorestechServerDIContainerGenerator().Create(TestModDirectory.ForUnitTestModDirectory);
            var itemStackFactory = ServerContext.ItemStackFactory;
            var worldBlockDatastore = ServerContext.WorldBlockDatastore;
            
            //パケットでプレイヤーインベントリを生成
            
            //ホットバーにアイテムとしてのブロックをセットする
            var slot = PlayerInventoryConst.HotBarSlotToInventorySlot(HotBarSlot);
            var inventory = serviceProvider.GetService<IPlayerInventoryDataStore>().GetInventoryData(PlayerId);
            inventory.MainOpenableInventory.SetItem(slot, itemStackFactory.Create(BlockItemId, 4));
            
            
            //ブロックを置く
            packet.GetPacketResponse(CreateUseHotBarProtocol(2, 4, BlockDirection.North));
            packet.GetPacketResponse(CreateUseHotBarProtocol(2, 5, BlockDirection.East));
            packet.GetPacketResponse(CreateUseHotBarProtocol(2, 6, BlockDirection.South));
            packet.GetPacketResponse(CreateUseHotBarProtocol(2, 7, BlockDirection.West));
            
            //ブロックの向きをチェック
            Assert.AreEqual(BlockDirection.North, worldBlockDatastore.GetBlockDirection(new Vector3Int(2, 4)));
            Assert.AreEqual(BlockDirection.East, worldBlockDatastore.GetBlockDirection(new Vector3Int(2, 5)));
            Assert.AreEqual(BlockDirection.South, worldBlockDatastore.GetBlockDirection(new Vector3Int(2, 6)));
            Assert.AreEqual(BlockDirection.West, worldBlockDatastore.GetBlockDirection(new Vector3Int(2, 7)));
        }
        
        private List<byte> CreateUseHotBarProtocol(int x, int y, BlockDirection blockDirection)
        {
            var placeInfo = new List<PlaceInfo>
            {
                new()
                {
                    Position = new Vector3Int(x, y),
                    Direction = blockDirection,
                    VerticalDirection = BlockVerticalDirection.Horizontal
                }
            };
            
            return MessagePackSerializer
                .Serialize(new SendPlaceHotBarBlockProtocolMessagePack(PlayerId, HotBarSlot, placeInfo))
                .ToList();
        }
    }
}