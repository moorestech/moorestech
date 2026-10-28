using System;
using Game.Block.Blocks.Machine.State;
using Newtonsoft.Json.Linq;

namespace Game.Block.Blocks.CleanRoom.Machine.YieldPreview
{
    // 抽選に使った役割別の率だけを保存し、ロード時に計算し直さない
    // Persist role-specific draw rates without recomputing them on load
    internal static class CleanRoomDrawRecordSave
    {
        internal static JObject Capture(IRealizedOutputDrawRecord record)
        {
            return record switch
            {
                null => null,
                WaferGradePreview wafer => new JObject { ["kind"] = "waferGrade", ["maxGrade"] = wafer.MaxGrade, ["promotionChance"] = wafer.PromotionChance },
                LayerExposurePreview layer => new JObject { ["kind"] = "layerExposure", ["layerFailRate"] = layer.LayerFailRate },
                DicingPreview dicing => new JObject { ["kind"] = "dicing", ["dieLossRate"] = dicing.DieLossRate },
                _ => throw new InvalidOperationException($"保存できない抽選記録です:{record.GetType().Name}"),
            };
        }

        internal static IRealizedOutputDrawRecord Restore(JObject record)
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
}
