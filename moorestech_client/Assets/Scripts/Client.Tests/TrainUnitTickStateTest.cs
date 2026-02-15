using Client.Game.InGame.Train.Unit;
using NUnit.Framework;

namespace Client.Tests
{
    public class TrainUnitTickStateTest
    {
        [Test]
        public void CreateTickUnifiedId_PacksTickAndSequence()
        {
            // tick と sequence が 64bit に正しく詰められることを確認する。
            // Verify tick and sequence are packed into 64-bit unified id.
            const uint tick = 123;
            const uint tickSequenceId = 456;
            var unifiedId = TrainTickUnifiedIdUtility.CreateTickUnifiedId(tick, tickSequenceId);

            Assert.AreEqual(((ulong)tick << 32) | tickSequenceId, unifiedId);
        }

        [Test]
        public void RecordAppliedTickUnifiedId_WithTickAndSequence_UpdatesState()
        {
            // 明示指定した tickUnifiedId が適用状態に反映されることを確認する。
            // Ensure explicit tickUnifiedId is reflected in applied state.
            var state = new TrainUnitTickState();
            state.RecordAppliedTickUnifiedId(10, 500);

            Assert.AreEqual(10, state.GetTick());
            Assert.AreEqual(
                TrainTickUnifiedIdUtility.CreateTickUnifiedId(10, 500),
                state.GetAppliedTickUnifiedId());
        }

        [Test]
        public void RecordAppliedTickUnifiedId_DoesNotRollbackOnOlderValue()
        {
            // 古い tickUnifiedId で更新しても巻き戻らないことを確認する。
            // Ensure state never rolls back when an older unified id is recorded.
            var state = new TrainUnitTickState();
            state.RecordAppliedTickUnifiedId(30, 100);
            state.RecordAppliedTickUnifiedId(29, uint.MaxValue);

            Assert.AreEqual(30, state.GetTick());
            Assert.AreEqual(
                TrainTickUnifiedIdUtility.CreateTickUnifiedId(30, 100),
                state.GetAppliedTickUnifiedId());
        }

        [Test]
        public void AdvanceTick_MovesToNextTickAndResetsSequenceToZero()
        {
            // tick 進行時は sequence を 0 にして次tickへ移動することを確認する。
            // Ensure tick advance moves to next tick with sequence reset to zero.
            var state = new TrainUnitTickState();
            state.RecordAppliedTickUnifiedId(40, 999);
            state.AdvanceTick();

            Assert.AreEqual(41, state.GetTick());
            Assert.AreEqual(
                TrainTickUnifiedIdUtility.CreateTickUnifiedId(41, 0),
                state.GetAppliedTickUnifiedId());
        }
    }
}
