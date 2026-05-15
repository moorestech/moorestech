using Game.Train.Unit;
using NUnit.Framework;

namespace Tests.UnitTest.Game
{
    public class TrainCarRidingInputBufferTest
    {
        [Test]
        public void SetLatestInput_OlderTickFromSamePlayer_IsIgnored()
        {
            var buffer = new TrainCarRidingInputBuffer();
            var carId = new TrainCarInstanceId(10);

            buffer.SetLatestInput(new TrainCarRidingInputBuffer.TrainCarRidingInputState(1, carId, 5, true, false, false, false));
            buffer.SetLatestInput(new TrainCarRidingInputBuffer.TrainCarRidingInputState(1, new TrainCarInstanceId(20), 4, false, false, true, false));

            var latestInput = GetSingleInput(buffer);
            Assert.AreEqual(carId, latestInput.RidingTrainCarInstanceId, "同一プレイヤーの古い tick 入力で最新入力が上書きされています。");
            Assert.IsTrue(latestInput.W, "古い tick 入力で W 状態が失われています。");
        }

        [Test]
        public void SetLatestInput_SameTickFromSamePlayer_UsesLastPayload()
        {
            var buffer = new TrainCarRidingInputBuffer();

            buffer.SetLatestInput(new TrainCarRidingInputBuffer.TrainCarRidingInputState(1, new TrainCarInstanceId(10), 5, true, false, false, false));
            buffer.SetLatestInput(new TrainCarRidingInputBuffer.TrainCarRidingInputState(1, new TrainCarInstanceId(20), 5, false, false, true, false));

            var latestInput = GetSingleInput(buffer);
            Assert.AreEqual(new TrainCarInstanceId(20), latestInput.RidingTrainCarInstanceId, "同 tick の後着入力が採用されていません。");
            Assert.IsTrue(latestInput.S, "同 tick の後着入力のボタン状態が保持されていません。");
        }

        private static TrainCarRidingInputBuffer.TrainCarRidingInputState GetSingleInput(TrainCarRidingInputBuffer buffer)
        {
            foreach (var input in buffer.GetLatestInputs())
            {
                return input;
            }

            Assert.Fail("入力が 1 件も保持されていません。");
            return default;
        }
    }
}
