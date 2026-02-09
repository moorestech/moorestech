using System.Collections.Generic;
using Game.Train.Unit;
using SharedTrainUnitSnapshotHashCalculator = Game.Train.Unit.TrainUnitSnapshotHashCalculator;

namespace Client.Game.InGame.Train.Unit
{
    // クライアント側APIは維持しつつ共有実装へ委譲する
    // Keep client API compatibility and delegate to shared implementation
    public static class TrainUnitSnapshotHashCalculator
    {
        // 共有側のハッシュ計算ロジックをそのまま利用する
        // Use shared hash calculator logic without duplicating implementation
        public static uint Compute(IReadOnlyList<TrainUnitSnapshotBundle> bundles)
        {
            return SharedTrainUnitSnapshotHashCalculator.Compute(bundles);
        }
    }
}
