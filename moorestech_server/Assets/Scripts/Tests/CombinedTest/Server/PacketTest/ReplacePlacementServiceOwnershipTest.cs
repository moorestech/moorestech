using System;
using Game.Block.Interface;
using Game.Construction;
using Game.Context;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Server.Protocol;
using Server.Protocol.PacketResponse.Util.Construction;
using Tests.Module.TestMod;
using UnityEngine;
using static Tests.CombinedTest.Server.PacketTest.PlaceBlockProtocolTestSupport;

namespace Tests.CombinedTest.Server.PacketTest
{
    /// <summary>
    /// 張替えを引き受けるかの判定がサービス側にあり、共通の設置プロトコルがブロックの種類を知らないことを検証する
    /// Verifies that deciding whether a cell is a replace target lives in the service, so the shared placement protocol never knows the block kind
    /// </summary>
    public class ReplacePlacementServiceOwnershipTest
    {
        [Test]
        public void 張替えサービスはインターフェースで解決される()
        {
            var (_, serviceProvider) = CreateServer();

            // プロトコルはこの口しか知らないので、実装差し替えだけで別ブロックの張替えを足せる
            // The protocol knows only this port, so another block's replace can be added by swapping the implementation alone
            var service = serviceProvider.GetService<IReplacePlacementService>();
            Assert.IsInstanceOf<BeltReplacePlacementService>(service);
        }

        [Test]
        public void 張替えを引き受ける組み合わせはサービスが答える()
        {
            var (_, serviceProvider) = CreateServer();
            var service = serviceProvider.GetService<IReplacePlacementService>();

            // ベルトファミリーの同ロール同士だけを引き受け、ファミリー外とロール違いは引き受けない
            // Only belt family members sharing a role are owned; non-members and mismatched roles are not
            Assert.IsTrue(service.CanReplace(ForUnitTestModBlockId.GearBeltConveyor, ForUnitTestModBlockId.LargeGearBeltConveyor));
            Assert.IsFalse(service.CanReplace(ForUnitTestModBlockId.MachineId, ForUnitTestModBlockId.LargeGearBeltConveyor));
            Assert.IsFalse(service.CanReplace(ForUnitTestModBlockId.GearBeltConveyor, ForUnitTestModBlockId.MachineId));
            Assert.IsFalse(service.CanReplace(ForUnitTestModBlockId.GearBeltConveyor, ForUnitTestModBlockId.SmallGearBeltConveyorSplitter));
        }

        [Test]
        public void プロトコルの張替え委譲はCanReplaceの答えと一致する()
        {
            var (packet, serviceProvider) = CreateServer();
            var service = serviceProvider.GetService<IReplacePlacementService>();
            var held = ForUnitTestModBlockId.LargeGearBeltConveyor;
            UnlockBlock(serviceProvider, held);
            GrantRequiredItems(serviceProvider, held, 1);

            // 引き受ける組み合わせ: 委譲されて差し替わる
            // An owned combination: delegated and swapped
            var ownedPosition = new Vector3Int(90, 0, 90);
            ServerContext.WorldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.GearBeltConveyor, ownedPosition, BlockDirection.North, Array.Empty<BlockCreateParam>(), out _);
            Assert.IsTrue(service.CanReplace(ForUnitTestModBlockId.GearBeltConveyor, held));
            packet.GetPacketResponse(CreateReplacePayload(held, ownedPosition, BlockDirection.North), new PacketResponseContext(null));
            Assert.AreEqual(held, ServerContext.WorldBlockDatastore.GetBlock(ownedPosition).BlockId);

            // 引き受けない組み合わせ: 委譲されず既設が同じインスタンスのまま残る
            // A combination nobody owns: never delegated, and the existing block survives as the very same instance
            var unownedPosition = new Vector3Int(92, 0, 92);
            ServerContext.WorldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.MachineId, unownedPosition, BlockDirection.North, Array.Empty<BlockCreateParam>(), out var machine);
            Assert.IsFalse(service.CanReplace(ForUnitTestModBlockId.MachineId, held));
            packet.GetPacketResponse(CreateReplacePayload(held, unownedPosition, BlockDirection.North), new PacketResponseContext(null));
            var survived = ServerContext.WorldBlockDatastore.GetBlock(unownedPosition);
            Assert.AreEqual(ForUnitTestModBlockId.MachineId, survived.BlockId);
            Assert.AreEqual(machine.BlockInstanceId, survived.BlockInstanceId);
        }
    }
}
