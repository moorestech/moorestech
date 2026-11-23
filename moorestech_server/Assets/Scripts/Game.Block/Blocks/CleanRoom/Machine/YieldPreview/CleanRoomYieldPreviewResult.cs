using System;
using System.Collections.Generic;
using Game.Block.Blocks.Machine.State;
using Newtonsoft.Json.Linq;

namespace Game.Block.Blocks.CleanRoom.Machine.YieldPreview
{
    /// <summary>
    ///     選択工程の抽選に使う値。役割ごとの型で、その役割に要る値だけを持つ。抽選の確定時にも同じ型で記録する
    ///     Values a draw of the selected process uses; one type per role, carrying only that role's values, and recorded in the same type when a draw is fixed
    /// </summary>
    public abstract class CleanRoomYieldPreviewResult : IRealizedOutputDrawRecord
    {
        // 種別はこのファイルの型に閉じる
        // The kinds are closed to the types in this file
        private protected CleanRoomYieldPreviewResult()
        {
        }

        // 新しい役割も保存の実装を必須とする
        // Every new role must implement its own serialization
        internal abstract JObject CaptureDrawRecord();

        internal static CleanRoomYieldPreviewResult RestoreDrawRecord(JObject drawRecord)
        {
            if (drawRecord == null) return null;
            return (string)drawRecord["kind"] switch
            {
                "layerExposure" => new LayerExposurePreview(Required<double>("layerFailRate")),
                "dicing" => new DicingPreview(Required<double>("dieLossRate"), Required<int>("qualityTier"), RequiredDoubles("levelRates"), Required<double>("failedRate")),
                _ => throw new InvalidOperationException($"未対応の保存抽選記録です:{drawRecord["kind"]}"),
            };

            #region Internal
            T Required<T>(string key)
            {
                if (drawRecord[key] == null || drawRecord[key].Type == JTokenType.Null) throw new InvalidOperationException($"保存抽選記録の必須キーがありません:{key}");
                return drawRecord[key].Value<T>();
            }

            double[] RequiredDoubles(string key)
            {
                if (drawRecord[key] == null || drawRecord[key].Type == JTokenType.Null) throw new InvalidOperationException($"保存抽選記録の必須キーがありません:{key}");
                return drawRecord[key].ToObject<double[]>();
            }
            #endregion
        }
    }

    public sealed class LayerExposurePreview : CleanRoomYieldPreviewResult
    {
        public double LayerFailRate { get; }

        public LayerExposurePreview(double layerFailRate)
        {
            LayerFailRate = layerFailRate;
        }

        internal override JObject CaptureDrawRecord() => new JObject { ["kind"] = "layerExposure", ["layerFailRate"] = LayerFailRate };
    }

    // ダイシング: 損失率とレベル別割合
    // Dicing: die loss rate plus the per-level and failed-die rates blended by the approach weight over survivors
    public sealed class DicingPreview : CleanRoomYieldPreviewResult
    {
        public double DieLossRate { get; }
        public int QualityTier { get; }
        public IReadOnlyList<double> LevelRates { get; }
        public double FailedRate { get; }

        public DicingPreview(double dieLossRate, int qualityTier, IReadOnlyList<double> levelRates, double failedRate)
        {
            DieLossRate = dieLossRate;
            QualityTier = qualityTier;
            LevelRates = levelRates;
            FailedRate = failedRate;
        }

        internal override JObject CaptureDrawRecord() => new JObject
        {
            ["kind"] = "dicing",
            ["dieLossRate"] = DieLossRate,
            ["qualityTier"] = QualityTier,
            ["levelRates"] = new JArray(LevelRates),
            ["failedRate"] = FailedRate,
        };
    }
}
