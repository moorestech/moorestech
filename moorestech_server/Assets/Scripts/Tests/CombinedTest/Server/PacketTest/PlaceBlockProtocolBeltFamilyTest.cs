using System;
using System.Collections.Generic;
using Core.Master;
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
        public void BeltPlacementRejectsVerticalOrientationsAndAllowsSingleSlopesTest()
        {
            var (packet, services) = CreateServer();
            int index = 0;
            foreach (var family in MasterHolder.BlockMaster.Blocks.BeltConveyorFamilies)
            {
                var straight = MasterHolder.BlockMaster.GetBlockId(family.StraightBlockGuid);
                UnlockBlock(services, straight);
                foreach (var guid in new Guid?[] { family.StraightBlockGuid, family.UpBlockGuid, family.DownBlockGuid })
                {
                    if (!guid.HasValue) continue;
                    var blockId = MasterHolder.BlockMaster.GetBlockId(guid.Value);
                    GrantRequiredItems(services, blockId, 4);
                    // 各形状を単独設置し、上下姿勢だけが拒否されることを確認する。
                    // Place each shape separately and verify that only vertical orientations are rejected.
                    foreach (BlockDirection direction in Enum.GetValues(typeof(BlockDirection)))
                    {
                        var position = new Vector3Int(100 + index++ * 4, 0, 100);
                        packet.GetPacketResponse(CreatePlacePayload(new List<PlaceInfo>
                        {
                            new() { BlockId = blockId, Position = position, Direction = direction },
                        }), Tests.Util.PlayerIdentity.BoundPacketContext.Bind(PlayerId));
                        bool horizontal = BlockDirection.North <= direction && direction <= BlockDirection.West;
                        Assert.AreEqual(horizontal, ServerContext.WorldBlockDatastore.Exists(position), $"{blockId}: {direction}");
                    }
                }
            }
        }

        [Test]
        public void DirectPlacementRejectsVerticalBeltsAndKeepsOtherBlocksOrientationsTest()
        {
            CreateServer();
            int index = 0;
            var ids = new[] { ForUnitTestModBlockId.BeltConveyorId, ForUnitTestModBlockId.GearBeltConveyor,
                ForUnitTestModBlockId.TestGearBeltConveyorUp, ForUnitTestModBlockId.TestGearBeltConveyorDown,
                ForUnitTestModBlockId.SmallGearBeltConveyor, ForUnitTestModBlockId.GearBeltConveyorSplitter };
            // 通信を通らない設置にも同じ制約を適用し、worldへ登録しない。
            // Apply the same constraint to direct placement without registering rejected blocks.
            foreach (var id in ids)
            foreach (BlockDirection direction in Enum.GetValues(typeof(BlockDirection)))
            {
                var position = new Vector3Int(100 + index++ * 4, 0, 200);
                bool placed = ServerContext.WorldBlockDatastore.TryAddBlock(id, position, direction, Array.Empty<BlockCreateParam>(), out var block);
                bool horizontal = BlockDirection.North <= direction && direction <= BlockDirection.West;
                Assert.AreEqual(horizontal, placed, $"{id}: {direction}");
                Assert.AreEqual(horizontal, ServerContext.WorldBlockDatastore.Exists(position));
                if (!horizontal) Assert.IsNull(block);
            }
            // ベルト以外の上下姿勢は既存どおり許可する。
            // Preserve vertical placement for blocks other than belts.
            foreach (var direction in new[] { BlockDirection.UpNorth, BlockDirection.DownNorth })
                Assert.IsTrue(ServerContext.WorldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.ChestId,
                    new Vector3Int(100 + index++ * 4, 0, 200), direction, Array.Empty<BlockCreateParam>(), out _));
        }

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
            packet.GetPacketResponse(CreatePlacePayload(placeInfos), Tests.Util.PlayerIdentity.BoundPacketContext.Bind(PlayerId));

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
            packet.GetPacketResponse(CreatePlacePayload(placeInfos), Tests.Util.PlayerIdentity.BoundPacketContext.Bind(PlayerId));
            Assert.IsFalse(ServerContext.WorldBlockDatastore.Exists(new Vector3Int(20, 0, 10)));

            // 直線を解放すると坂を設置できる
            // Unlocking the straight block allows slope placement
            UnlockBlock(serviceProvider, ForUnitTestModBlockId.GearBeltConveyor);
            packet.GetPacketResponse(CreatePlacePayload(placeInfos), Tests.Util.PlayerIdentity.BoundPacketContext.Bind(PlayerId));
            Assert.IsTrue(ServerContext.WorldBlockDatastore.Exists(new Vector3Int(20, 0, 10)));
            Assert.AreEqual(ForUnitTestModBlockId.TestGearBeltConveyorUp,
                ServerContext.WorldBlockDatastore.GetBlock(new Vector3Int(20, 0, 10)).BlockId);
        }
    }
}
