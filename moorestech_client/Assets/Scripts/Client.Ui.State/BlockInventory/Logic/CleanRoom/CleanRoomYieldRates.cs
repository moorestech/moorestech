using System;
using System.Collections.Generic;

namespace Client.Ui.State.BlockInventory.Logic.CleanRoom
{
    /// <summary>
    /// 役割ごとの歩留まりの値（Web の CleanRoomYieldFetchedData の role ごとの判別 union）
    /// Per-role yield values (the per-role discriminated union of Web CleanRoomYieldFetchedData)
    /// </summary>
    public abstract class CleanRoomYieldRates
    {
        private protected CleanRoomYieldRates()
        {
        }

        public static CleanRoomYieldRates LayerExposure(double layerFailRate)
        {
            return new LayerExposureYieldRates(layerFailRate);
        }

        public static CleanRoomYieldRates Dicing(double dieLossRate, int qualityTier, IReadOnlyList<double> levelRates, double failedRate)
        {
            return new DicingYieldRates(dieLossRate, qualityTier, levelRates, failedRate);
        }
    }

    // 露光（EUV）は層の失敗率だけ
    // Exposure (EUV) carries only the layer failure rate
    public sealed class LayerExposureYieldRates : CleanRoomYieldRates
    {
        public double LayerFailRate { get; }

        internal LayerExposureYieldRates(double layerFailRate)
        {
            LayerFailRate = layerFailRate;
        }
    }

    public sealed class DicingYieldRates : CleanRoomYieldRates
    {
        public double DieLossRate { get; }
        public int QualityTier { get; }
        public IReadOnlyList<double> LevelRates { get; }
        public double FailedRate { get; }

        internal DicingYieldRates(double dieLossRate, int qualityTier, IReadOnlyList<double> levelRates, double failedRate)
        {
            DieLossRate = dieLossRate;
            QualityTier = qualityTier;
            LevelRates = levelRates;
            FailedRate = failedRate;
        }
    }

    // 取得済みの歩留まり。出力待ちの確定済み抽選の有無を sealed 派生で分ける（判断記録 D-5）
    // Fetched yield; sealed subtypes split whether a fixed draw awaits output (D-5)
    public abstract class CleanRoomYieldFetched
    {
        public CleanRoomYieldRates Current { get; }

        private protected CleanRoomYieldFetched(CleanRoomYieldRates current)
        {
            Current = current;
        }

        public static CleanRoomYieldFetched WithoutPendingDraw(CleanRoomYieldRates current)
        {
            return new SettledCleanRoomYield(current);
        }

        // 出力待ちの確定済み抽選は親と同じ役割の値だけを持つ。役割が違う組は作らせない
        // A fixed draw awaiting output carries only the parent's role; a mismatched pair is refused at construction
        public static CleanRoomYieldFetched WithPendingDraw(CleanRoomYieldRates current, CleanRoomYieldRates pendingDraw)
        {
            if (current.GetType() != pendingDraw.GetType()) throw new ArgumentException($"pending draw role {pendingDraw.GetType().Name} differs from {current.GetType().Name}");
            return new PendingDrawCleanRoomYield(current, pendingDraw);
        }
    }

    public sealed class SettledCleanRoomYield : CleanRoomYieldFetched
    {
        internal SettledCleanRoomYield(CleanRoomYieldRates current) : base(current)
        {
        }
    }

    public sealed class PendingDrawCleanRoomYield : CleanRoomYieldFetched
    {
        public CleanRoomYieldRates PendingDraw { get; }

        internal PendingDrawCleanRoomYield(CleanRoomYieldRates current, CleanRoomYieldRates pendingDraw) : base(current)
        {
            PendingDraw = pendingDraw;
        }
    }
}
