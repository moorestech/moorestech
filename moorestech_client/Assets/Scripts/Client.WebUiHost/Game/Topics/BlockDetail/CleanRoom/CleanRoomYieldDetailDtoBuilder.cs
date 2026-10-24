using System;
using Server.Protocol.PacketResponse;

namespace Client.WebUiHost.Game.Topics.BlockDetail.CleanRoom
{
    /// <summary>
    ///     抽選の中身の役割ごとのビューから、webui に送る欄を組み立てる
    ///     Builds the webui section from the per-role draw-detail view
    /// </summary>
    public static class CleanRoomYieldDetailDtoBuilder
    {
        public static void Apply(BlockInventoryDto dto, BlockNetworkInfoCache cache)
        {
            // silent-ok: 未取得・消失・読めない応答の間は欄を出さない / 消失・読めない応答はポーラーが警告を1回出して問い合わせを止め（次に開き直すと再開）、未取得は次の1秒で欄が出る
            if (!cache.TryGetCleanRoomYield(out var view)) return;
            dto.CleanRoomYield = Build(view);
        }

        // 役割名の文字列はこの網羅 switch の1か所でだけ作る。役割の無いレシピと稼働できない機械は null（停止理由は部屋欄が出す）
        // Role name strings are made only in this exhaustive switch; null for role-less recipes and machines that cannot operate (the room section shows the halt reason)
        public static CleanRoomYieldDetailDto Build(CleanRoomYieldView view)
        {
            return view switch
            {
                CleanRoomYieldWaferGradeView waferGrade => new CleanRoomYieldDetailDto { Role = "waferGrade", MaxGrade = waferGrade.WaferGrade.MaxGrade, PromotionChance = waferGrade.WaferGrade.PromotionChance },
                CleanRoomYieldLayerExposureView layerExposure => new CleanRoomYieldDetailDto { Role = "layerExposure", LayerFailRate = layerExposure.LayerExposure.LayerFailRate },
                CleanRoomYieldDicingView dicing => new CleanRoomYieldDetailDto { Role = "dicing", DieLossRate = dicing.Dicing.DieLossRate },
                CleanRoomYieldNoRoleView _ => null,
                CleanRoomYieldNotOperableView _ => null,
                _ => throw new ArgumentOutOfRangeException(nameof(view), view.GetType().Name, "未対応の抽選の中身の種別 / Unsupported yield view"),
            };
        }
    }
}
