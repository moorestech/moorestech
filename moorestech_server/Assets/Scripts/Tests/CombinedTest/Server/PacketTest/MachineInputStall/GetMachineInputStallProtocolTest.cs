using System.Text.RegularExpressions;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using MessagePack;
using NUnit.Framework;
using Server.Boot;
using Server.Protocol;
using Server.Protocol.PacketResponse;
using Tests.Module.TestMod;
using Tests.Util;
using UnityEngine;
using UnityEngine.TestTools;
using static Tests.CombinedTest.Core.Machine.Upgrade.LevelInheritanceTestHelper;
using static Tests.Module.TestMod.ForUnitTestLevelInheritanceIds;
using static Tests.Util.BeltWorldTestUtil;

namespace Tests.CombinedTest.Server.PacketTest.MachineInputStall
{
    public class GetMachineInputStallProtocolTest
    {
        [Test]
        public void StalledMachineReturnsReasonAndWaitingItemTest()
        {
            var packet = CreatePacket();
            var (block, inventory, _) = PlaceMachine(Vector3Int.zero);
            MachineRecipeSelectTestUtil.SelectRecipe(block, Recipe(InheritRecipeGuid));
            inventory.SetItem(0, Create(Source(1), 1));

            // Lv1 入りのスロットへブロック搬入で Lv2 が断られると、理由と待っている品が返る
            // When a block inflow of Lv2 is refused by a slot holding Lv1, the reason and the waiting item come back
            inventory.InsertItem(Create(Source(2), 1), InsertItemContext.Empty);
            var view = ReadView<MachineInputStalledView>(Invoke(packet, block.BlockInstanceId));
            Assert.AreEqual(MachineInputStallReasonKind.MixedLevel, view.Stall.Reason);
            Assert.AreEqual(Source(2).AsPrimitive(), view.Stall.WaitingItemId);
        }

        [Test]
        public void IdleMachineReturnsNotStalledTest()
        {
            var packet = CreatePacket();
            var (block, _, _) = PlaceMachine(Vector3Int.zero);

            // 何も届いていない機械は止まっていない
            // A machine nothing has arrived at is not stalled
            ReadView<MachineInputNotStalledView>(Invoke(packet, block.BlockInstanceId));
        }

        [Test]
        public void NonMachineBlockReturnsNotStalledWithWarningTest()
        {
            var packet = CreatePacket();
            var chest = Place(ForUnitTestModBlockId.ChestId, new Vector3Int(3, 0, 0), BlockDirection.North);

            // クライアントは機械にだけ問い合わせる。別種のブロックは食い違いとして警告し、止まっていない扱いで返す
            // The client only asks machines; any other block is a mismatch, warned about and answered as not stalled
            LogAssert.Expect(LogType.Warning, new Regex(@"\[GetMachineInputStall\] block .* has no MachineInputLevelStallComponent"));
            ReadView<MachineInputNotStalledView>(Invoke(packet, chest.BlockInstanceId));
        }

        [Test]
        public void UnknownBlockReturnsBlockNotFoundTest()
        {
            var packet = CreatePacket();

            // 存在しないブロックIDは BlockNotFound を返し、ビューを持たない
            // A missing block ID returns BlockNotFound, which has no view
            var response = Invoke(packet, new BlockInstanceId(999999));
            Assert.AreEqual(MachineInputStallResponseKind.BlockNotFound, response.Kind);
            Assert.IsFalse(response.TryGetView(out _));
        }

        [Test]
        public void MalformedResponsesAreUnreadableWithWarningTest()
        {
            // 判別子が Stalled なのに中身が欠けた応答は、警告を出して読めない扱いにする
            // A Stalled response missing its details warns and is unreadable
            var missing = GetMachineInputStallProtocol.ResponseMachineInputStallMessagePack.CreateStalled(MachineInputStallReasonKind.MixedLevel, 1);
            missing.Stall = null;
            LogAssert.Expect(LogType.Warning, new Regex(@"\[GetMachineInputStall\] Stalled response is unreadable"));
            Assert.IsFalse(missing.TryGetView(out _));

            // 未知の理由（版違いの wire）も wire を通して同じ扱い
            // An unknown reason (a mismatched wire) is treated the same through the wire
            var unknownReason = GetMachineInputStallProtocol.ResponseMachineInputStallMessagePack.CreateStalled(MachineInputStallReasonKind.MixedLevel, 1);
            unknownReason.Stall.Reason = (MachineInputStallReasonKind)99;
            var received = MessagePackSerializer.Deserialize<GetMachineInputStallProtocol.ResponseMachineInputStallMessagePack>(MessagePackSerializer.Serialize(unknownReason));
            LogAssert.Expect(LogType.Warning, new Regex(@"\[GetMachineInputStall\] Stalled response is unreadable"));
            Assert.IsFalse(received.TryGetView(out _));
        }

        private static PacketResponseCreator CreatePacket()
        {
            var (packet, _) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            return packet;
        }

        private static GetMachineInputStallProtocol.ResponseMachineInputStallMessagePack Invoke(PacketResponseCreator packet, BlockInstanceId id)
        {
            var request = new GetMachineInputStallProtocol.RequestMachineInputStallMessagePack(id);
            var responseBytes = packet.GetPacketResponse(MessagePackSerializer.Serialize(request), Tests.Util.PlayerIdentity.BoundPacketContext.Bind(1));
            return MessagePackSerializer.Deserialize<GetMachineInputStallProtocol.ResponseMachineInputStallMessagePack>(responseBytes[0]);
        }

        // 応答を種別ごとのビューで読み、期待した型であることを確かめる
        // Read the response as its per-kind view and check it is the expected one
        private static TView ReadView<TView>(GetMachineInputStallProtocol.ResponseMachineInputStallMessagePack response) where TView : MachineInputStallView
        {
            Assert.IsTrue(response.TryGetView(out var view), $"kind={response.Kind}");
            Assert.IsInstanceOf<TView>(view);
            return (TView)view;
        }
    }
}
