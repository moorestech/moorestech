using System;
using System.Collections.Generic;
using System.Linq;
using Game.Block.Blocks.CleanRoom.Machine.YieldPreview;
using Response = Server.Protocol.PacketResponse.GetCleanRoomYieldInfoProtocol.ResponseCleanRoomYieldInfoMessagePack;

namespace Server.Protocol.PacketResponse
{
    /// <summary>
    ///     サーバーの抽選の中身の報告を応答へ写す（クライアントは Game.Block の型を持たない）
    ///     Maps the server's draw-detail report onto the response, so the client never holds Game.Block types
    /// </summary>
    public static class CleanRoomYieldResponseFactory
    {
        public static Response Create(CleanRoomYieldPreviewReport report)
        {
            // 報告の種別と最終の内訳の型ごとに factory へ写す。数字を持たない種別は判別子だけ（停止理由は部屋欄が出す）
            // Map each report kind and final-odds type onto its factory; kinds without numbers carry only the discriminator (the room section shows the halt reason)
            return report switch
            {
                CleanRoomNoRoleYieldReport _ => Response.CreateNoRole(),
                CleanRoomNotOperableYieldReport _ => Response.CreateNotOperable(),
                CleanRoomOddsYieldReport { Odds: LayerExposureFinalOdds layer } withOdds => Response.CreateLayerExposure(RequireSameRole<LayerExposurePreview>(withOdds, withOdds.Next).LayerFailRate,
                    withOdds.PendingDraw == null ? null : new LayerExposureYieldMessagePack(RequireSameRole<LayerExposurePreview>(withOdds, withOdds.PendingDraw).LayerFailRate),
                    new LayerExposureOddsMessagePack(layer.FailedRate), ToImprovementMessagePacks(withOdds.Improvements)),
                CleanRoomOddsYieldReport { Odds: DicingFinalOdds dicing } withOdds => Response.CreateDicing(ToDicingMessagePack(RequireSameRole<DicingPreview>(withOdds, withOdds.Next)),
                    withOdds.PendingDraw == null ? null : ToDicingMessagePack(RequireSameRole<DicingPreview>(withOdds, withOdds.PendingDraw)),
                    new DicingOddsMessagePack(dicing.OutputCount, dicing.LevelRates.ToArray(), dicing.FailedRate), ToImprovementMessagePacks(withOdds.Improvements)),
                _ => throw new ArgumentOutOfRangeException(nameof(report), report.GetType().Name, "未対応の抽選の中身の報告 / Unsupported draw-detail report"),
            };

            #region Internal

            // 次の抽選と確定済み抽選は最終の内訳と同じ役割のはず（C7 で外す古い項目のためだけの確認）。違えば部品の約束の破れなので止める
            // The next and the fixed draw share the final odds' role (checked only for the old fields removed in C7); a mismatch breaks the component's contract, so stop
            static T RequireSameRole<T>(CleanRoomOddsYieldReport withOdds, CleanRoomYieldPreviewResult result) where T : CleanRoomYieldPreviewResult
            {
                return result as T ?? throw new InvalidOperationException($"抽選の中身の役割が最終の内訳と違います odds={withOdds.Odds.GetType().Name} result={result.GetType().Name}");
            }

            // 改善は並びのまま載せる
            // Improvements keep their order
            static List<CleanRoomYieldImprovementMessagePack> ToImprovementMessagePacks(IReadOnlyList<CleanRoomYieldImprovement> improvements)
            {
                return improvements.Select(ToImprovementMessagePack).ToList();
            }

            static DicingYieldMessagePack ToDicingMessagePack(DicingPreview dicing)
            {
                return new DicingYieldMessagePack(dicing.DieLossRate, dicing.QualityTier, dicing.LevelRates.ToArray(), dicing.FailedRate);
            }

            static CleanRoomYieldImprovementMessagePack ToImprovementMessagePack(CleanRoomYieldImprovement improvement)
            {
                var levelDeltas = improvement.LevelRateDeltas.ToArray();
                return improvement switch
                {
                    CleanRoomRoomClassUpImprovement classUp => CleanRoomYieldImprovementMessagePack.CreateRoomClassUp(classUp.TargetClassName, levelDeltas, classUp.FailedRateDelta),
                    CleanRoomAddQualityModuleImprovement addModule => CleanRoomYieldImprovementMessagePack.CreateAddQualityModule(addModule.ModuleTier, levelDeltas, addModule.FailedRateDelta),
                    _ => throw new ArgumentOutOfRangeException(nameof(improvement), improvement.GetType().Name, "未対応の改善の種別 / Unsupported improvement kind"),
                };
            }

            #endregion
        }
    }
}
