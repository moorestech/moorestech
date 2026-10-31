using Core.Update;
using Game.Block.Blocks.CleanRoom.Machine;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Block.Interface.Extension;
using Game.Block.Interface.State;
using Game.Context;
using MessagePack;
using NUnit.Framework;
using Tests.Module.TestMod;
using UniRx;
using UnityEngine;
using static Tests.CombinedTest.Core.CleanRoom.Machine.CleanRoomMachineTestHelpers;

namespace Tests.CombinedTest.Core.CleanRoom.Machine
{
    public class CleanRoomMachineOperabilityTest
    {
        [Test]
        public void MachineOutsideAnyRoomReportsNotEnclosedTest()
        {
            CleanRoomHatchTest.CreateServer();
            var machine = CleanRoomHatchTest.PlaceBlock(ForUnitTestModBlockId.CleanRoomMachineId, new Vector3Int(10, 0, 10));
            GameUpdater.UpdateOneTick();

            Assert.AreEqual(CleanRoomOperability.NotEnclosed, ReadOperability(machine));
        }

        [Test]
        public void OperabilityFollowsRoomClassAndBreachTest()
        {
            CleanRoomHatchTest.CreateServer();
            var filter = BuildSmallCleanRoomWithFilter();
            var machine = CleanRoomHatchTest.PlaceBlock(ForUnitTestModBlockId.CleanRoomMachineId, new Vector3Int(2, 1, 1));
            var notifications = 0;
            machine.GetComponent<CleanRoomOperabilityStateComponent>().OnChangeBlockState.Subscribe(_ => notifications++);

            // 検出直後の部屋はOutから始まるので、クラス不足として止まる
            // A freshly detected room starts at Out, so the machine halts for an insufficient class
            GameUpdater.UpdateOneTick();
            Assert.AreEqual(CleanRoomOperability.ClassOut, ReadOperability(machine));

            // 清浄機を回してクラスに届けば稼働可になる
            // Running the purifier until a class is reached makes the machine operable
            for (var i = 0; i < 3000 && ReadOperability(machine) != CleanRoomOperability.Operable; i++) TickRoom(filter, machine);
            Assert.AreEqual(CleanRoomOperability.Operable, ReadOperability(machine));

            // 壁を壊すと未密閉に変わる
            // Breaking a wall turns it to not enclosed
            ServerContext.WorldBlockDatastore.RemoveBlock(new Vector3Int(2, 2, 0), BlockRemoveReason.ManualRemove);
            GameUpdater.UpdateOneTick();
            Assert.AreEqual(CleanRoomOperability.NotEnclosed, ReadOperability(machine));

            // 毎tick同じ値を押し込んでも、通知は変化した3回だけ
            // Although the same value is pushed every tick, only the three changes notify
            Assert.AreEqual(3, notifications);
        }

        private static CleanRoomOperability ReadOperability(IBlock machine)
        {
            var bytes = machine.GetBlockState().CurrentStateDetails[CleanRoomOperabilityStateDetail.BlockStateDetailKey];
            return MessagePackSerializer.Deserialize<CleanRoomOperabilityStateDetail>(bytes).Operability;
        }
    }
}
