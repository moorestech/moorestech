using System;
using Game.Train.Unit;
using Mooresmaster.Model.RidableSeatModule;
using Mooresmaster.Model.TrainModule;
using Tests.Util;

namespace Tests.UnitTest.PlayerRiding
{
    // 乗車システムのテスト用ヘルパ。座席付き車両やデータストアの生成をまとめる。
    // Test helpers for the riding system: builds seated cars and datastores.
    public static class RidingTestHelper
    {
        // 指定座席数のマスタを持つ単体 TrainCar を生成する。
        // Creates a standalone TrainCar whose master has the given seat count.
        public static TrainCar CreateTrainCarWithSeats(int seatCount)
        {
            // TrainCar コンストラクタが ServerContext を参照するため DI 環境を先に用意する
            // The TrainCar constructor reads ServerContext, so set up the DI environment first.
            TrainTestHelper.CreateEnvironment();
            var master = CreateTrainCarMasterWithSeats(seatCount);
            return new TrainCar(master, true);
        }

        // 座席数 seatCount の TrainCarMasterElement を生成する。
        // Builds a TrainCarMasterElement with seatCount ridable seats.
        public static TrainCarMasterElement CreateTrainCarMasterWithSeats(int seatCount)
        {
            var seats = new RidableSeat[seatCount];
            for (var i = 0; i < seatCount; i++)
            {
                seats[i] = new RidableSeat(0f, 0f, 0f);
            }
            return new TrainCarMasterElement(1, Guid.Empty, Guid.Empty, null, 320, 0, 1, 5, "None", 0f, null, null, seats);
        }
    }
}
