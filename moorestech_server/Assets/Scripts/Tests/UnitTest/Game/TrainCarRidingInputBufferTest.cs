using System;
using System.Collections.Generic;
using Game.PlayerRiding.Interface;
using Game.Train.Unit;
using NUnit.Framework;
using UniRx;

namespace Tests.UnitTest.Game
{
    public class TrainCarRidingInputBufferTest
    {
        [Test]
        public void SetLatestInput_LastPayloadFromSamePlayer_IsUsed()
        {
            var buffer = new TrainCarRidingInputBuffer();

            buffer.SetLatestInput(new TrainCarRidingInputBuffer.TrainCarRidingInputState(1, 5, true, false, false, false));
            buffer.SetLatestInput(new TrainCarRidingInputBuffer.TrainCarRidingInputState(1, 4, false, false, true, false));

            var latestInput = GetSingleInput(buffer);
            Assert.IsTrue(latestInput.MoveBackward, "後着入力の後退入力状態が保持されていません。");
        }

        [Test]
        public void SetLatestInput_SameTickFromSamePlayer_UsesLastPayload()
        {
            var buffer = new TrainCarRidingInputBuffer();

            buffer.SetLatestInput(new TrainCarRidingInputBuffer.TrainCarRidingInputState(1, 5, true, false, false, false));
            buffer.SetLatestInput(new TrainCarRidingInputBuffer.TrainCarRidingInputState(1, 5, false, false, true, false));

            var latestInput = GetSingleInput(buffer);
            Assert.IsTrue(latestInput.MoveBackward, "同 tick の後着入力の後退入力状態が保持されていません。");
        }

        [Test]
        public void RidingStateChange_ClearsMoveAndBranchInput()
        {
            var ridingDatastore = new TestPlayerRidingDatastore();
            var buffer = new TrainCarRidingInputBuffer(ridingDatastore);
            buffer.SetLatestInput(new TrainCarRidingInputBuffer.TrainCarRidingInputState(1, 5, true, true, false, false));

            ridingDatastore.Publish(new RidingStateChange(1, RidingStateChangeType.Dismount, null));

            Assert.AreEqual(0, buffer.GetLatestMoveInputs().Count, "降車後に W/S 状態が残っています。");
            Assert.AreEqual(0, buffer.ConsumeBranchSelectionInputs().Count, "降車後に A/D 押下イベントが残っています。");
        }

        private static TrainCarRidingInputBuffer.TrainCarRidingMoveInputState GetSingleInput(TrainCarRidingInputBuffer buffer)
        {
            foreach (var input in buffer.GetLatestMoveInputs())
            {
                return input;
            }

            Assert.Fail("入力が 1 件も保持されていません。");
            return default;
        }

        private sealed class TestPlayerRidingDatastore : IPlayerRidingDatastore
        {
            public IObservable<RidingStateChange> OnRidingStateChanged => _subject;
            private readonly Subject<RidingStateChange> _subject = new();

            public bool TryGetRidingState(int playerId, out RidingState ridingState)
            {
                ridingState = null;
                return false;
            }

            public RideActionResult TryRide(int playerId, IRidableIdentifier identifier, out int assignedSeatIndex)
            {
                assignedSeatIndex = -1;
                return RideActionResult.RidableNotFound;
            }

            public RideActionResult TryDismount(int playerId)
            {
                return RideActionResult.NotRiding;
            }

            public IReadOnlyList<int> OnRidableRemoved(IRidableIdentifier identifier)
            {
                return Array.Empty<int>();
            }

            public bool EvaluateOnLogin(int playerId)
            {
                return false;
            }

            public List<PlayerRidingSaveData> GetSaveData()
            {
                return new List<PlayerRidingSaveData>();
            }

            public void LoadSaveData(IReadOnlyList<PlayerRidingSaveData> saveData)
            {
            }

            public void Publish(RidingStateChange change)
            {
                _subject.OnNext(change);
            }
        }
    }
}
