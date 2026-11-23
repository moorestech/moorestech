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
                CleanRoomOddsYieldReport { Odds: LayerExposureFinalOdds layer } withOdds => Response.CreateLayerExposure(new LayerExposureOddsMessagePack(layer.FailedRate), ToImprovementMessagePacks(withOdds.Improvements)),
                CleanRoomOddsYieldReport { Odds: DicingFinalOdds dicing } withOdds => Response.CreateDicing(new DicingOddsMessagePack(dicing.OutputCount, dicing.LevelRates.ToArray(), dicing.FailedRate), ToImprovementMessagePacks(withOdds.Improvements)),
                _ => throw new ArgumentOutOfRangeException(nameof(report), report.GetType().Name, "未対応の抽選の中身の報告 / Unsupported draw-detail report"),
            };

            #region Internal

            // 改善は並びのまま載せる
            // Improvements keep their order
            static List<CleanRoomYieldImprovementMessagePack> ToImprovementMessagePacks(IReadOnlyList<CleanRoomYieldImprovement> improvements)
            {
                return improvements.Select(ToImprovementMessagePack).ToList();
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
