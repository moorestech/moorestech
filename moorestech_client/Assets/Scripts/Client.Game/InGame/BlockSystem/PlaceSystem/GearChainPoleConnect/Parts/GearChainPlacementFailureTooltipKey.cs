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
        // 素材不足は行を作らず不足リストのまま関門へ渡す。行にした瞬間に同一アイテムの畳み込みが効かなくなる
        // A material shortage is never turned into lines here; it goes to the gate as data, since lines can no longer be folded per item
        private static bool IsMaterialShortage(string failureReason)
        {
            return failureReason == GearChainPlacementEvaluator.NoItemError;
        }

        // チェーン判定が素材不足で落ちたフレームか。判定の中身はこの型の外へ出さない
        // Whether the chain judgement failed on a material shortage; the judgement itself never leaves this type
        public static bool IsChainMaterialShortage(GearChainPoleExtendPreviewData chainPreview)
        {
            if (!chainPreview.IsValid || chainPreview.IsPlaceable) return false;
            return IsMaterialShortage(chainPreview.FailureReason);
        }

        // 可:行なし／素材不足:行なし（関門が出す）／他:理由1行
        // Placeable: none / material shortage: none (the gate emits it) / otherwise: one reason line
        public static IReadOnlyList<TooltipLine> BuildFailureLines(bool isPlaceable, string failureReason)
        {
            if (isPlaceable) return Array.Empty<TooltipLine>();
            if (IsMaterialShortage(failureReason)) return Array.Empty<TooltipLine>();
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
