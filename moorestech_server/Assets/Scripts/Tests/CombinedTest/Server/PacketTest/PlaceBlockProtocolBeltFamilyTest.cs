using System.Collections.Generic;
using Game.Block.Interface;
using Game.Context;
using Game.World.Interface.DataStore;
using NUnit.Framework;
using Server.Protocol;
using Server.Protocol.PacketResponse;
using Tests.Module.TestMod;
using UnityEngine;
using static Tests.CombinedTest.Server.PacketTest.PlaceBlockProtocolTestSupport;

namespace Tests.CombinedTest.Server.PacketTest
{
    /// <summary>
    /// セル毎BlockId方式でのベルトファミリー設置・直線ブロックunlock判定のテスト
    /// Tests per-cell BlockId placement and straight-block unlock resolution for belt families
    /// </summary>
    public class PlaceBlockProtocolBeltFamilyTest
    {
        [Test]
        public void セル毎に異なるBlockIdを一括設置できる()
        {
            var (packet, serviceProvider) = CreateServer();
            // 素材付与（歯車ベルト1セット×2）
            // Grant two cost sets of the gear belt family materials
            GrantRequiredItems(serviceProvider, ForUnitTestModBlockId.GearBeltConveyor, 2);
            UnlockBlock(serviceProvider, ForUnitTestModBlockId.GearBeltConveyor);

            var placeInfos = new List<PlaceInfo>
            {
                new()
                {
                    Position = new Vector3Int(10, 0, 10), Direction = BlockDirection.North,
                    VerticalDirection = BlockVerticalDirection.Horizontal, BlockId = ForUnitTestModBlockId.GearBeltConveyor,
                },
                new()
                {
                    Position = new Vector3Int(10, 0, 11), Direction = BlockDirection.North,
                    VerticalDirection = BlockVerticalDirection.Up, BlockId = ForUnitTestModBlockId.TestGearBeltConveyorUp,
                },
            };
            packet.GetPacketResponse(CreatePlacePayload(placeInfos), new PacketResponseContext(null));

            Assert.IsTrue(ServerContext.WorldBlockDatastore.Exists(new Vector3Int(10, 0, 10)));
            Assert.IsTrue(ServerContext.WorldBlockDatastore.Exists(new Vector3Int(10, 0, 11)));
            Assert.AreEqual(ForUnitTestModBlockId.TestGearBeltConveyorUp,
                ServerContext.WorldBlockDatastore.GetBlock(new Vector3Int(10, 0, 11)).BlockId);
        }

        [Test]
        public void 坂ブロックの設置可否はファミリー直線のunlock状態で決まる()
        {
            var (packet, serviceProvider) = CreateServer();
            GrantRequiredItems(serviceProvider, ForUnitTestModBlockId.TestGearBeltConveyorUp, 1);

            // ファミリー直線が未解放なら坂も設置不可
            // A slope cannot be placed while the family straight block is locked
            LockBlock(serviceProvider, ForUnitTestModBlockId.GearBeltConveyor);
            var placeInfos = new List<PlaceInfo>
            {
                new()
                {
                    Position = new Vector3Int(20, 0, 10), Direction = BlockDirection.North,
                    VerticalDirection = BlockVerticalDirection.Up, BlockId = ForUnitTestModBlockId.TestGearBeltConveyorUp,
                },
            };
            packet.GetPacketResponse(CreatePlacePayload(placeInfos), new PacketResponseContext(null));
            Assert.IsFalse(ServerContext.WorldBlockDatastore.Exists(new Vector3Int(20, 0, 10)));

            // 直線を解放すると坂を設置できる
            // Unlocking the straight block allows slope placement
            UnlockBlock(serviceProvider, ForUnitTestModBlockId.GearBeltConveyor);
            packet.GetPacketResponse(CreatePlacePayload(placeInfos), new PacketResponseContext(null));
            Assert.IsTrue(ServerContext.WorldBlockDatastore.Exists(new Vector3Int(20, 0, 10)));
            Assert.AreEqual(ForUnitTestModBlockId.TestGearBeltConveyorUp,
                ServerContext.WorldBlockDatastore.GetBlock(new Vector3Int(20, 0, 10)).BlockId);
        }
    }
}
