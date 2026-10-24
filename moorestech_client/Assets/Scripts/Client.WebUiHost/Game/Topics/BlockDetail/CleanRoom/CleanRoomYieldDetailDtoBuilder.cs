using System;
using Client.Game.InGame.BlockInfoPolling;
using Server.Protocol.PacketResponse;

namespace Client.WebUiHost.Game.Topics.BlockDetail.CleanRoom
{
    /// <summary>
    ///     役割別ビューをWeb表示へ変換
    ///     Converts per-role views into web display data
    /// </summary>
    public static class CleanRoomYieldDetailDtoBuilder
    {
        public static void Apply(BlockInventoryDto dto, BlockNetworkInfoCache cache)
        {
            // 抽選の中身を追うのは室内機械だけ
            // Only interior machines follow the draw details
            if (cache.CleanRoomTarget != CleanRoomDetailTarget.Machine) return;
            var fetchState = cache.GetCleanRoomYield(out var view);
            dto.CleanRoomYield = Build(fetchState, view);
        }

        // 取得できていない間は取得状態だけの欄にする。取得済みなら役割ごとの欄（役割の無いレシピと稼働できない機械は欄なし）
        // While not fetched, the section carries only the fetch state; once fetched, the per-role section (none for role-less recipes and machines that cannot operate)
        public static CleanRoomYieldDetailDto Build(BlockInfoFetchState fetchState, CleanRoomYieldView latestView)
        {
            if (fetchState != BlockInfoFetchState.Fetched) return CleanRoomYieldDetailDto.CreateUnavailable(fetchState);
            return BuildFetched(latestView);
        }

        // 役割名の文字列はこの網羅 switch の1か所でだけ作る。出力待ちの確定済み抽選は同じ役割の子欄に載せる。停止理由は部屋欄が出す
        // Role name strings are made only in this exhaustive switch; a fixed draw waiting for output goes into a same-role child; the room section shows the halt reason
        private static CleanRoomYieldDetailDto BuildFetched(CleanRoomYieldView view)
        {
            return view switch
            {
                CleanRoomYieldWaferGradeView waferGrade => CleanRoomYieldDetailDto.CreateWaferGrade(waferGrade.WaferGrade.MaxGrade, waferGrade.WaferGrade.PromotionChance, waferGrade.PendingDraw == null ? null : CleanRoomYieldPendingDrawDto.CreateWaferGrade(waferGrade.PendingDraw.MaxGrade, waferGrade.PendingDraw.PromotionChance)),
                CleanRoomYieldLayerExposureView layerExposure => CleanRoomYieldDetailDto.CreateLayerExposure(layerExposure.LayerExposure.LayerFailRate, layerExposure.PendingDraw == null ? null : CleanRoomYieldPendingDrawDto.CreateLayerExposure(layerExposure.PendingDraw.LayerFailRate)),
                CleanRoomYieldDicingView dicing => CleanRoomYieldDetailDto.CreateDicing(dicing.Dicing.DieLossRate, dicing.PendingDraw == null ? null : CleanRoomYieldPendingDrawDto.CreateDicing(dicing.PendingDraw.DieLossRate)),
                CleanRoomYieldNoRoleView _ => null,
                CleanRoomYieldNotOperableView _ => null,
                _ => throw new ArgumentOutOfRangeException(nameof(view), view.GetType().Name, "未対応の抽選の中身の種別 / Unsupported yield view"),
            };
        }
    }
}
