using System;
using System.Collections.Generic;
using Core.Master.CleanRoomRoles;

namespace Game.Block.Blocks.CleanRoom.Machine.YieldPreview
{
    /// <summary>
    ///     画面に出す最終の内訳。Lv別の最終割合と失敗を足すと1になる（EUV はLv別が空で、失敗＝層の失敗率）
    ///     Final odds shown on screen; the final per-level rates plus the failure sum to 1 (EUV has no levels and its failure is the layer fail rate)
    /// </summary>
    public abstract class CleanRoomFinalOdds
    {
        public IReadOnlyList<double> LevelRates { get; }
        public double FailedRate { get; }

        private protected CleanRoomFinalOdds(IReadOnlyList<double> levelRates, double failedRate)
        {
            LevelRates = levelRates;
            FailedRate = failedRate;
        }

        // 役割と抽選の率から最終の内訳を作る。組の食い違いは呼び出し側の約束違反なので止める
        // Build final odds from the role and the draw rates; a mismatched pair breaks the caller's contract, so stop
        public static CleanRoomFinalOdds Of(CleanRoomRecipeRole role, CleanRoomYieldPreviewResult rates)
        {
            return (role, rates) switch
            {
                (LayerExposureRecipeRole _, LayerExposurePreview layer) => new LayerExposureFinalOdds(layer.LayerFailRate),
                (DicingRecipeRole dicingRole, DicingPreview dicing) => DicingFinalOdds.From(dicingRole.Dicing.DiesPerWafer, dicing),
                _ => throw new ArgumentOutOfRangeException(nameof(rates), $"{role.GetType().Name}/{rates.GetType().Name}", "最終の内訳を作れない役割と率の組 / No final odds for this role and rate pair"),
            };
        }
    }

    public sealed class LayerExposureFinalOdds : CleanRoomFinalOdds
    {
        public LayerExposureFinalOdds(double layerFailRate) : base(Array.Empty<double>(), layerFailRate)
        {
        }
    }

    public sealed class DicingFinalOdds : CleanRoomFinalOdds
    {
        // レシピの出力個数（ウェハ1枚のダイ数）
        // The recipe's output count (dies per wafer)
        public int OutputCount { get; }

        private DicingFinalOdds(int outputCount, IReadOnlyList<double> levelRates, double failedRate) : base(levelRates, failedRate)
        {
            OutputCount = outputCount;
        }

        // Lv N の最終割合＝(1−損失率)×Lv N の割合。失敗は残り全部（抽選が残りを失敗ダイにするのと同じ）
        // Final Lv N = (1 - loss) x Lv N rate; the failure is the whole remainder, as the draw turns the remainder into failed dies
        public static DicingFinalOdds From(int outputCount, DicingPreview dicing)
        {
            var survivalRate = 1d - dicing.DieLossRate;
            var levelRates = new double[dicing.LevelRates.Count];
            var levelSum = 0d;
            for (var i = 0; i < levelRates.Length; i++)
            {
                levelRates[i] = survivalRate * dicing.LevelRates[i];
                levelSum += levelRates[i];
            }
            return new DicingFinalOdds(outputCount, levelRates, 1d - levelSum);
        }
    }
}
