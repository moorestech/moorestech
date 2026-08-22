using System;
using System.Collections.Generic;
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
        // 接続判定の結果を行へ変換する。可なら行なし、不可なら理由キー1行
        // Turns a connection judgement into lines: none when placeable, one reason-key line otherwise
        public static IReadOnlyList<TooltipLine> BuildFailureLines(bool isPlaceable, string failureReason)
        {
            if (isPlaceable) return Array.Empty<TooltipLine>();
            return new[] { new TooltipLine(ToKey(failureReason)) };
        }

        public static LocalizationKey ToKey(string failureReason)
        {
            return failureReason switch
            {
                GearChainPlacementEvaluator.TooFarError => LocalizationKeys.Ui.Tooltip.PlaceGearChainTooFar,
                GearChainPlacementEvaluator.AlreadyConnectedError => LocalizationKeys.Ui.Tooltip.PlaceGearChainAlreadyConnected,
                GearChainPlacementEvaluator.ConnectionLimitError => LocalizationKeys.Ui.Tooltip.PlaceGearChainConnectionLimit,
                GearChainPlacementEvaluator.NoItemError => LocalizationKeys.Ui.Tooltip.PlaceGearChainNoItem,
                // 上記以外（未解放・サーバー側のみの理由）はクライアントの接続判定では発生しないため既定文言へ
                // Everything else (not-unlocked, server-only reasons) never arises in client connection judgement, so fall back
                _ => LocalizationKeys.Ui.Tooltip.PlaceGearChainFailed,
            };
        }
    }
}
