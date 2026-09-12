using Core.Update;
using NUnit.Framework;

namespace Tests.Util
{
    // 確率抽選が GameRandom を何回引いたかを状態の一致で固定する。抽選を private Random へ戻す変異はここで必ず落ちる
    // Pins how many times a probabilistic site drew from GameRandom by matching its state; reverting a site to a private Random always fails here
    public static class GameRandomDrawAssert
    {
        private const ulong Seed = 20260912UL;

        // 期待状態は抽選の実行前に作る。作る過程そのものが GameRandom を消費するため
        // Build the expected state before running the site, because building it consumes GameRandom itself
        public static ulong[] StateAfterDraws(int draws)
        {
            GameRandom.Reseed(Seed);
            for (var i = 0; i < draws; i++) GameRandom.NextUlong();
            return GameRandom.ExportState();
        }

        // 計測開始。期待状態と同じシードへ戻してから抽選の入口を1回呼ぶ
        // Starts the measurement: returns to the same seed as the expected state, then the site is invoked once
        public static void BeginDrawCount()
        {
            GameRandom.Reseed(Seed);
        }

        public static void AssertDrawn(ulong[] expectedState, string message)
        {
            CollectionAssert.AreEqual(expectedState, GameRandom.ExportState(), message);
        }

        // 引く回数がマスタ設定で決まる入口用。引いたか引いていないかだけを固定する
        // For sites whose draw count is decided by master data; pins only whether a draw happened at all
        public static ulong[] CurrentState()
        {
            return GameRandom.ExportState();
        }

        public static void AssertDrewAtLeastOnce(ulong[] beforeState, string message)
        {
            CollectionAssert.AreNotEqual(beforeState, GameRandom.ExportState(), message);
        }
    }
}
