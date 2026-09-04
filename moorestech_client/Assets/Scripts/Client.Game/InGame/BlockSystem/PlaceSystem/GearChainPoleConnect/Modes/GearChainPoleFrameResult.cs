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

        // このフレームの不可理由行。プッシュはsystem側
        // This frame's block-reason lines; the system pushes them
        public readonly IReadOnlyList<TooltipLine> FeedbackLines;

        // 落とし先を持たない不足素材（ポール自身の建設コスト）。行にせず関門へ運び畳むだけ
        // Shortages without a fallback (the pole's own construction cost); they travel as data and are only folded by the gate
        public readonly IReadOnlyList<ConstructionMaterialShortage> MaterialShortages;

        // 落とし先キーを持つ不足素材（チェーン素材）。0件なら関門が汎用文言1行へ落とす
        // Shortages with a fallback key (the chain material); at zero entries the gate emits one generic line
        public readonly IReadOnlyList<ConstructionMaterialShortage> FallbackMaterialShortages;

        // 上記の落とし先キー。nullは「このフレームはチェーン素材不足ではない」
        // The fallback key above; null means this frame has no chain material shortage
        public readonly LocalizationKey? MaterialShortageFallbackKey;

        /// <summary>
        /// 不足素材を関門へ流す。畳むだけの分を先に積み、落とし先付きは0件でも汎用文言へ落とす
        /// Push the shortages through the gate: the fold-only ones first, then the keyed ones which fall back even at zero entries
        /// </summary>
        public void PushMaterialShortages(PlacementFeedback feedback)
        {
            feedback.AddMaterialShortages(MaterialShortages);
            if (MaterialShortageFallbackKey.HasValue) feedback.AddMaterialShortagesOrFallback(FallbackMaterialShortages, MaterialShortageFallbackKey.Value);
        }

        /// <summary>
        /// 起点を維持（または送信なしで変更）してプレビューだけ更新する
        /// Keep (or change without sending) the source and update only the preview
        /// </summary>
        public static GearChainPoleFrameResult Show(IGearChainPoleConnectAreaCollider sourcePole, GearChainPolePreviewCommand preview)
        {
            return Show(sourcePole, preview, Array.Empty<TooltipLine>());
        }

        /// <summary>
        /// プレビューに加えて不可理由の行を返す
        /// Update the preview and also return the placement-block reason lines
        /// </summary>
        public static GearChainPoleFrameResult Show(IGearChainPoleConnectAreaCollider sourcePole, GearChainPolePreviewCommand preview, IReadOnlyList<TooltipLine> feedbackLines)
        {
            return new GearChainPoleFrameResult(sourcePole, false, preview, null, null, feedbackLines, Array.Empty<ConstructionMaterialShortage>(), Array.Empty<ConstructionMaterialShortage>(), null);
        }

        /// <summary>
        /// 不可理由の行に加えて、畳むだけの不足素材を関門へ渡す
        /// Return the block-reason lines plus fold-only shortages destined for the gate
        /// </summary>
        public static GearChainPoleFrameResult ShowWithMaterialShortages(IGearChainPoleConnectAreaCollider sourcePole, GearChainPolePreviewCommand preview, IReadOnlyList<TooltipLine> feedbackLines, IReadOnlyList<ConstructionMaterialShortage> materialShortages)
        {
            return new GearChainPoleFrameResult(sourcePole, false, preview, null, null, feedbackLines, materialShortages, Array.Empty<ConstructionMaterialShortage>(), null);
        }

        /// <summary>
        /// 畳むだけの不足素材と、落とし先キー付きの不足素材を別枠で関門へ渡す
        /// Hand the gate the fold-only shortages and the fallback-keyed shortages in separate slots
        /// </summary>
        public static GearChainPoleFrameResult ShowWithMaterialShortages(IGearChainPoleConnectAreaCollider sourcePole, GearChainPolePreviewCommand preview, IReadOnlyList<TooltipLine> feedbackLines, IReadOnlyList<ConstructionMaterialShortage> materialShortages, IReadOnlyList<ConstructionMaterialShortage> fallbackMaterialShortages, LocalizationKey materialShortageFallbackKey)
        {
            return new GearChainPoleFrameResult(sourcePole, false, preview, null, null, feedbackLines, materialShortages, fallbackMaterialShortages, materialShortageFallbackKey);
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

        private GearChainPoleFrameResult(IGearChainPoleConnectAreaCollider nextSourcePole, bool invalidatePendingRequest, GearChainPolePreviewCommand preview, GearChainPoleExtendSendCommand? extendSend, GearChainConnectSendCommand? chainConnectSend, IReadOnlyList<TooltipLine> feedbackLines, IReadOnlyList<ConstructionMaterialShortage> materialShortages, IReadOnlyList<ConstructionMaterialShortage> fallbackMaterialShortages, LocalizationKey? materialShortageFallbackKey)
        {
            NextSourcePole = nextSourcePole;
            InvalidatePendingRequest = invalidatePendingRequest;
            Preview = preview;
            ExtendSend = extendSend;
            ChainConnectSend = chainConnectSend;
            FeedbackLines = feedbackLines;
            MaterialShortages = materialShortages;
            FallbackMaterialShortages = fallbackMaterialShortages;
            MaterialShortageFallbackKey = materialShortageFallbackKey;
        }
    }
}
