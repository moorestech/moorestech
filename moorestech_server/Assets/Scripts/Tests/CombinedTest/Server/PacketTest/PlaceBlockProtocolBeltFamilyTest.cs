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
    /// セル毎BlockId方式でのベルトファミリー設置・ファミリー代表unlock判定のテスト
    /// Tests for per-cell BlockId placement and belt-family representative unlock resolution
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
        public void バリアントの設置可否はファミリー代表のunlock状態で決まる()
        {
            var (packet, serviceProvider) = CreateServer();
            GrantRequiredItems(serviceProvider, ForUnitTestModBlockId.GearBeltConveyor3, 1);

            // 代表（GearBeltConveyor）が未解放なら長尺バリアントも設置不可
            // A length variant cannot be placed while the family representative is locked
            LockBlock(serviceProvider, ForUnitTestModBlockId.GearBeltConveyor);
            var placeInfos = new List<PlaceInfo>
            {
                new()
                {
                    Position = new Vector3Int(20, 0, 10), Direction = BlockDirection.North,
                    VerticalDirection = BlockVerticalDirection.Horizontal, BlockId = ForUnitTestModBlockId.GearBeltConveyor3,
                },
            };
            packet.GetPacketResponse(CreatePlacePayload(placeInfos), new PacketResponseContext(null));
            Assert.IsFalse(ServerContext.WorldBlockDatastore.Exists(new Vector3Int(20, 0, 10)));

            // 代表を解放すると長尺バリアントが設置できる
            // Unlocking the representative allows the variant placement
            UnlockBlock(serviceProvider, ForUnitTestModBlockId.GearBeltConveyor);
            packet.GetPacketResponse(CreatePlacePayload(placeInfos), new PacketResponseContext(null));
            Assert.IsTrue(ServerContext.WorldBlockDatastore.Exists(new Vector3Int(20, 0, 10)));
        }
    }
}
