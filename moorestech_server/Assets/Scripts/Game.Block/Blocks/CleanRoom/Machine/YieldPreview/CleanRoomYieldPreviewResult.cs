using System;
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

        internal static CleanRoomYieldPreviewResult RestoreDrawRecord(JObject record)
        {
            if (record == null) return null;
            return (string)record["kind"] switch
            {
                "waferGrade" => new WaferGradePreview(Required<int>("maxGrade"), Required<double>("promotionChance")),
                "layerExposure" => new LayerExposurePreview(Required<double>("layerFailRate")),
                "dicing" => new DicingPreview(Required<double>("dieLossRate")),
                _ => throw new InvalidOperationException($"未対応の保存抽選記録です:{record["kind"]}"),
            };

            #region Internal
            T Required<T>(string key)
            {
                if (record[key] == null || record[key].Type == JTokenType.Null) throw new InvalidOperationException($"保存抽選記録の必須キーがありません:{key}");
                return record[key].Value<T>();
            }
            #endregion
        }
    }

    // 未選択か、役割表に無いレシピ
    // No recipe selected, or a recipe outside the role tables
    public sealed class NoRolePreview : CleanRoomYieldPreviewResult
    {
        public static readonly NoRolePreview Instance = new();

        private NoRolePreview()
        {
        }

        internal override JObject CaptureDrawRecord() => throw new InvalidOperationException("保存できない抽選記録です:NoRolePreview");
    }

    // 部屋が稼働を許さない（未密閉・クラス不足）。停止理由は A7 の状態が配る
    // The room does not allow operation (not enclosed or class out); A7's state carries the halt reason
    public sealed class NotOperablePreview : CleanRoomYieldPreviewResult
    {
        public static readonly NotOperablePreview Instance = new();

        private NotOperablePreview()
        {
        }

        internal override JObject CaptureDrawRecord() => throw new InvalidOperationException("保存できない抽選記録です:NotOperablePreview");
    }

    public sealed class WaferGradePreview : CleanRoomYieldPreviewResult
    {
        public int MaxGrade { get; }
        public double PromotionChance { get; }

        public WaferGradePreview(int maxGrade, double promotionChance)
        {
            MaxGrade = maxGrade;
            PromotionChance = promotionChance;
        }

        internal override JObject CaptureDrawRecord() => new JObject { ["kind"] = "waferGrade", ["maxGrade"] = MaxGrade, ["promotionChance"] = PromotionChance };
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

    public sealed class DicingPreview : CleanRoomYieldPreviewResult
    {
        public double DieLossRate { get; }

        public DicingPreview(double dieLossRate)
        {
            DieLossRate = dieLossRate;
        }

        internal override JObject CaptureDrawRecord() => new JObject { ["kind"] = "dicing", ["dieLossRate"] = DieLossRate };
    }
}
