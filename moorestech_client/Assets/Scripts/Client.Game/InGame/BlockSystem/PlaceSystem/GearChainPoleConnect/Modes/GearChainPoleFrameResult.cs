using System;
using System.Collections.Generic;
using Client.Game.InGame.BlockSystem.PlaceSystem.Feedback;
using Client.Game.InGame.BlockSystem.PlaceSystem.GearChainPoleConnect.Parts;
using Client.Game.InGame.BlockSystem.PlaceSystem.Util;
using Client.Game.InGame.UI.Tooltip;
using Mooresmaster.Localization.Generated;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.GearChainPoleConnect.Modes
{
    /// <summary>
    /// Decideの出力。次の起点・プレビュー指示・送信指示・不可理由の行のみで構成され、モードは状態や環境に一切書き込まない。
    /// Output of Decide: next source, preview command, send commands and reason lines only. Modes never write to state or environment.
    /// </summary>
    public readonly struct GearChainPoleFrameResult
    {
        public readonly IGearChainPoleConnectAreaCollider NextSourcePole;
        public readonly bool InvalidatePendingRequest;
        public readonly GearChainPolePreviewCommand Preview;
        public readonly GearChainPoleExtendSendCommand? ExtendSend;
        public readonly GearChainConnectSendCommand? ChainConnectSend;

        // このフレームの不可理由行。プッシュはPushFeedbackが担う
        // This frame's block-reason lines; PushFeedback is what pushes them
        public readonly IReadOnlyList<TooltipLine> FeedbackLines;

        // 落とし先を持たない不足素材（ポール自身の建設コスト）。行にせず畳むだけ
        // Shortages without a fallback (the pole's own construction cost); they are only folded, never turned into lines
        private readonly IReadOnlyList<ConstructionMaterialShortage> _ghostMaterialShortages;

        // 落とし先キーを持つ不足素材（チェーン素材）。0件なら汎用文言1行へ落とす
        // Shortages with a fallback key (the chain material); at zero entries they become one generic line
        private readonly IReadOnlyList<ConstructionMaterialShortage> _chainMaterialShortages;

        // 上記の落とし先キー。nullは「このフレームはチェーン素材不足ではない」。センチネルはこの型の外へ出さない
        // The fallback key above; null means this frame has no chain material shortage. The sentinel never leaves this type
        private readonly LocalizationKey? _chainMaterialShortageFallbackKey;

        /// <summary>
        /// 理由行と不足素材をツールチップへ流す唯一の経路。不足は同一アイテムを1行に畳む関門を必ず通る
        /// The single path pushing the reason lines and the shortages into the tooltip; shortages always cross the per-item folding gate
        /// </summary>
        public void PushFeedback(PlacementFeedback feedback)
        {
            foreach (var line in FeedbackLines) feedback.Add(line);

            feedback.AddMaterialShortages(_ghostMaterialShortages);
            if (_chainMaterialShortageFallbackKey.HasValue) feedback.AddMaterialShortagesOrFallback(_chainMaterialShortages, _chainMaterialShortageFallbackKey.Value);
        }

        /// <summary>
        /// 起点を維持（または送信なしで変更）して、プレビュー・理由行・不足素材を返す唯一のファクトリ
        /// The single factory keeping (or changing without sending) the source and returning the preview, the reason lines and the shortages
        /// chainPreviewはチェーン判定そのもの。素材不足のフレームだけ落とし先付きの不足枠が開く
        /// chainPreview is the chain judgement itself; only a material-shortage frame opens the fallback-keyed slot
        /// </summary>
        public static GearChainPoleFrameResult Show(IGearChainPoleConnectAreaCollider sourcePole, GearChainPolePreviewCommand preview, IReadOnlyList<TooltipLine> feedbackLines, GearChainPoleExtendPreviewData chainPreview, IReadOnlyList<ConstructionMaterialShortage> ghostMaterialShortages)
        {
            // チェーン素材不足なら、不足0件でも汎用文言へ落とせるよう不足の運搬自体を成立させる
            // When the chain material is short, the shortage channel opens even with zero entries so the generic wording can still appear
            if (!GearChainPlacementFailureTooltipKey.IsChainMaterialShortage(chainPreview)) return new GearChainPoleFrameResult(sourcePole, false, preview, null, null, feedbackLines, ghostMaterialShortages, Array.Empty<ConstructionMaterialShortage>(), null);

            return new GearChainPoleFrameResult(sourcePole, false, preview, null, null, feedbackLines, ghostMaterialShortages, chainPreview.MaterialShortages, LocalizationKeys.Ui.Tooltip.PlaceGearChainFailed);
        }

        /// <summary>
        /// クリックされたポールを新しい起点として選択する。進行中の応答は常に無効化する
        /// Select the clicked pole as the new source, always invalidating pending responses
        /// </summary>
        public static GearChainPoleFrameResult SelectSource(IGearChainPoleConnectAreaCollider pole)
        {
            return new GearChainPoleFrameResult(pole, true, GearChainPolePreviewCommand.Hidden, null, null, Array.Empty<TooltipLine>(), Array.Empty<ConstructionMaterialShortage>(), Array.Empty<ConstructionMaterialShortage>(), null);
        }

        /// <summary>
        /// 設置・延長リクエストを送信する。起点はクリアし、引き継ぎは応答取り込みで行う
        /// Send a place/extend request. The source is cleared; hand-off happens via response consumption
        /// </summary>
        public static GearChainPoleFrameResult SendExtend(GearChainPoleExtendSendCommand command)
        {
            return new GearChainPoleFrameResult(null, false, GearChainPolePreviewCommand.Hidden, command, null, Array.Empty<TooltipLine>(), Array.Empty<ConstructionMaterialShortage>(), Array.Empty<ConstructionMaterialShortage>(), null);
        }

        /// <summary>
        /// チェーン接続を送信して起点をクリアする。進行中の延長応答も無効化する
        /// Send a chain connect and clear the source, also invalidating pending extend responses
        /// </summary>
        public static GearChainPoleFrameResult SendChainConnect(GearChainConnectSendCommand command)
        {
            return new GearChainPoleFrameResult(null, true, GearChainPolePreviewCommand.Hidden, null, command, Array.Empty<TooltipLine>(), Array.Empty<ConstructionMaterialShortage>(), Array.Empty<ConstructionMaterialShortage>(), null);
        }

        private GearChainPoleFrameResult(IGearChainPoleConnectAreaCollider nextSourcePole, bool invalidatePendingRequest, GearChainPolePreviewCommand preview, GearChainPoleExtendSendCommand? extendSend, GearChainConnectSendCommand? chainConnectSend, IReadOnlyList<TooltipLine> feedbackLines, IReadOnlyList<ConstructionMaterialShortage> ghostMaterialShortages, IReadOnlyList<ConstructionMaterialShortage> chainMaterialShortages, LocalizationKey? chainMaterialShortageFallbackKey)
        {
            NextSourcePole = nextSourcePole;
            InvalidatePendingRequest = invalidatePendingRequest;
            Preview = preview;
            ExtendSend = extendSend;
            ChainConnectSend = chainConnectSend;
            FeedbackLines = feedbackLines;
            _ghostMaterialShortages = ghostMaterialShortages;
            _chainMaterialShortages = chainMaterialShortages;
            _chainMaterialShortageFallbackKey = chainMaterialShortageFallbackKey;
        }
    }
}
