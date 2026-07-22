using System;
using Core.Item.Interface;
using Core.Master;
using Game.Block.Blocks.BeltConveyor;
using Game.Block.Interface;
using Game.Block.Interface.Extension;
using Game.Context;
using Server.Protocol;
using Server.Protocol.PacketResponse;
using Tests.Module.TestMod;
using NUnit.Framework;
using UnityEngine;
using static Tests.CombinedTest.Server.PacketTest.PlaceBlockProtocolTestSupport;

namespace Tests.CombinedTest.Server.PacketTest
{
    /// <summary>
    /// リプレース設置の異常系（非ファミリー拒否・満杯失敗・通常設置不変）を検証する
    /// Verifies replace-placement edge cases (non-family rejection, full-inventory failure, unchanged normal placement)
    /// </summary>
    public class ReplaceBlockPlaceEdgeTest
    {
        // ファミリー外ブロックへのリプレースは拒否され、ブロックも素材も変化しないこと
        // Replacing a non-family block is rejected, leaving both the block and materials unchanged
        [Test]
        public void 非ファミリーブロックへのリプレースは拒否される()
        {
            var (packet, serviceProvider) = CreateServer();
            var pos = new Vector3Int(60, 0, 60);
            UnlockBlock(serviceProvider, ForUnitTestModBlockId.GearBeltConveyor);
            GrantRequiredItems(serviceProvider, ForUnitTestModBlockId.GearBeltConveyor, 1);

            // 機械ブロックはリプレースファミリー外
            // A machine block is outside the replace family
            ServerContext.WorldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.MachineId, pos, BlockDirection.North, Array.Empty<BlockCreateParam>(), out _);

            packet.GetPacketResponse(CreateReplacePayload(ForUnitTestModBlockId.GearBeltConveyor, pos, BlockDirection.East), new PacketResponseContext(null));

            Assert.AreEqual(ForUnitTestModBlockId.MachineId, ServerContext.WorldBlockDatastore.GetBlock(pos).BlockId);
            AssertRequiredItemsCount(serviceProvider, ForUnitTestModBlockId.GearBeltConveyor, 1);
        }

        // インベントリ満杯で事前検証に失敗し、旧ブロックと搬送品が失われないこと
        // Pre-validation fails when the inventory is full, so the old block and its transit item survive
        [Test]
        public void インベントリ満杯時は旧ブロックと搬送品が保持される()
        {
            var (packet, serviceProvider) = CreateServer();
            var pos = new Vector3Int(62, 0, 62);
            UnlockBlock(serviceProvider, ForUnitTestModBlockId.GearBeltConveyor);

            ServerContext.WorldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.GearBeltConveyor, pos, BlockDirection.North, Array.Empty<BlockCreateParam>(), out var oldBlock);
            var oldBelt = oldBlock.GetComponent<VanillaBeltConveyorComponent>();
            Assert.IsTrue(oldBelt.TryInsertItemWithRemainingRate(ForUnitTestItemId.ItemId2, ItemInstanceId.Create(), 0.5));

            // コスト外アイテムで全スロットを埋め、返却挿入の空きを消す
            // Fill every slot with a non-cost item so refund insertion has no room
            OccupyAllInventorySlots(serviceProvider, ForUnitTestItemId.ItemId1);

            packet.GetPacketResponse(CreateReplacePayload(ForUnitTestModBlockId.GearBeltConveyor, pos, BlockDirection.East), new PacketResponseContext(null));

            // 旧ブロックはDirectionそのまま、搬送品も旧ベルトに残る
            // The old block keeps its direction and the transit item remains on the old belt
            var block = ServerContext.WorldBlockDatastore.GetBlock(pos);
            Assert.AreEqual(BlockDirection.North, block.BlockPositionInfo.BlockDirection);
            Assert.IsTrue(HasItem(oldBelt, ForUnitTestItemId.ItemId2));
            AssertInventoryEmptyOfRequiredItems(serviceProvider, ForUnitTestModBlockId.GearBeltConveyor);

            #region Internal

            bool HasItem(VanillaBeltConveyorComponent belt, ItemId itemId)
            {
                foreach (var item in belt.BeltConveyorItems)
                {
                    if (item != null && item.ItemId == itemId) return true;
                }
                return false;
            }

            #endregion
        }

        // IsReplace=falseで既存セルに送ると従来通りスキップされ、素材も消費されないこと
        // With IsReplace=false, an occupied cell is skipped as before and no material is consumed
        [Test]
        public void 通常設置は既存ブロックをスキップして挙動が変わらない()
        {
            var (packet, serviceProvider) = CreateServer();
            var pos = new Vector3Int(64, 0, 64);
            UnlockBlock(serviceProvider, ForUnitTestModBlockId.GearBeltConveyor);
            GrantRequiredItems(serviceProvider, ForUnitTestModBlockId.GearBeltConveyor, 1);

            ServerContext.WorldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.BeltConveyorId, pos, BlockDirection.North, Array.Empty<BlockCreateParam>(), out _);

            // IsReplace=falseで別ブロックを同セルへ送る
            // Send a different block to the same cell with IsReplace=false
            var normalPlace = new System.Collections.Generic.List<PlaceInfo>
            {
                new()
                {
                    Position = pos,
                    Direction = BlockDirection.North,
                    VerticalDirection = BlockVerticalDirection.Horizontal,
                    BlockId = ForUnitTestModBlockId.GearBeltConveyor,
                    IsReplace = false,
                },
            };
            packet.GetPacketResponse(CreatePlacePayload(normalPlace), new PacketResponseContext(null));

            Assert.AreEqual(ForUnitTestModBlockId.BeltConveyorId, ServerContext.WorldBlockDatastore.GetBlock(pos).BlockId);
            AssertRequiredItemsCount(serviceProvider, ForUnitTestModBlockId.GearBeltConveyor, 1);
        }
    }
}
