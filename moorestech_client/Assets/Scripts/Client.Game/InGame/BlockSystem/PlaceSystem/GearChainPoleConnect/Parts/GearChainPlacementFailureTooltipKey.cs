using System;
using System.Collections.Generic;
using Client.Game.InGame.BlockSystem.PlaceSystem.Util;
using Client.Game.InGame.UI.Tooltip;
using Mooresmaster.Localization.Generated;
using Server.Protocol.PacketResponse.Util.GearChain;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.GearChainPoleConnect.Parts
{
    /// <summary>
    /// 歯車チェーン失敗理由をツールチップキーへ写像
    /// Maps a gear chain failure reason to a tooltip key
    /// </summary>
    public static class GearChainPlacementFailureTooltipKey
    {
        // 可:行なし／不足:素材ごと／他:理由1行
        // Placeable: none / Shortage: per-material / Other: one reason line
        public static IReadOnlyList<TooltipLine> BuildFailureLines(bool isPlaceable, string failureReason, IReadOnlyList<ConstructionMaterialShortage> materialShortages)
        {
            if (isPlaceable) return Array.Empty<TooltipLine>();
            if (failureReason == GearChainPlacementEvaluator.NoItemError) return ConstructionMaterialShortageLine.ToLines(materialShortages, LocalizationKeys.Ui.Tooltip.PlaceGearChainFailed);
            return new[] { new TooltipLine(ToKey(failureReason)) };
        }

        public static LocalizationKey ToKey(string failureReason)
        {
            return failureReason switch
            {
                GearChainPlacementEvaluator.TooFarError => LocalizationKeys.Ui.Tooltip.PlaceGearChainTooFar,
                GearChainPlacementEvaluator.AlreadyConnectedError => LocalizationKeys.Ui.Tooltip.PlaceGearChainAlreadyConnected,
                GearChainPlacementEvaluator.ConnectionLimitError => LocalizationKeys.Ui.Tooltip.PlaceGearChainConnectionLimit,
                // 素材不足(NoItemError)は名指しの行を素材ごとに積むためここでは写像しない
                // Material shortage (NoItemError) is not mapped here; it becomes one named line per material
                // 上記以外（未解放・サーバー側のみの理由）はクライアントの接続判定では発生しないため既定文言へ
                // Everything else (not-unlocked, server-only reasons) never arises in client connection judgement, so fall back
                _ => LocalizationKeys.Ui.Tooltip.PlaceGearChainFailed,
            };
        }
    }
}
