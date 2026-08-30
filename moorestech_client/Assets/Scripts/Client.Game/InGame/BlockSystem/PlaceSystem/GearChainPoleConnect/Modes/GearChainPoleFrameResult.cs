using System;
using System.Collections.Generic;
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

        // 不足素材は行にせずそのまま運ぶ。同一アイテムを畳む関門を通せるのはsystem側だけ
        // The shortages travel as data, never as lines, because only the system side can push them through the folding gate
        public readonly IReadOnlyList<ConstructionMaterialShortage> MaterialShortages;

        // 不足が1件も無いときの落とし先キー。nullは「このフレームは素材不足ではない」
        // The fallback key used when nothing is short; null means this frame has no material shortage at all
        public readonly LocalizationKey? MaterialShortageFallbackKey;

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
            return new GearChainPoleFrameResult(sourcePole, false, preview, null, null, feedbackLines, Array.Empty<ConstructionMaterialShortage>(), null);
        }

        /// <summary>
        /// 不可理由の行に加えて、関門へ渡す不足素材と落とし先キーを返す
        /// Return the block-reason lines plus the shortages and fallback key destined for the gate
        /// </summary>
        public static GearChainPoleFrameResult ShowWithMaterialShortages(IGearChainPoleConnectAreaCollider sourcePole, GearChainPolePreviewCommand preview, IReadOnlyList<TooltipLine> feedbackLines, IReadOnlyList<ConstructionMaterialShortage> materialShortages, LocalizationKey materialShortageFallbackKey)
        {
            return new GearChainPoleFrameResult(sourcePole, false, preview, null, null, feedbackLines, materialShortages, materialShortageFallbackKey);
        }

        /// <summary>
        /// クリックされたポールを新しい起点として選択する。進行中の応答は常に無効化する
        /// Select the clicked pole as the new source, always invalidating pending responses
        /// </summary>
        public static GearChainPoleFrameResult SelectSource(IGearChainPoleConnectAreaCollider pole)
        {
            return new GearChainPoleFrameResult(pole, true, GearChainPolePreviewCommand.Hidden, null, null, Array.Empty<TooltipLine>(), Array.Empty<ConstructionMaterialShortage>(), null);
        }

        /// <summary>
        /// 設置・延長リクエストを送信する。起点はクリアし、引き継ぎは応答取り込みで行う
        /// Send a place/extend request. The source is cleared; hand-off happens via response consumption
        /// </summary>
        public static GearChainPoleFrameResult SendExtend(GearChainPoleExtendSendCommand command)
        {
            return new GearChainPoleFrameResult(null, false, GearChainPolePreviewCommand.Hidden, command, null, Array.Empty<TooltipLine>(), Array.Empty<ConstructionMaterialShortage>(), null);
        }

        /// <summary>
        /// チェーン接続を送信して起点をクリアする。進行中の延長応答も無効化する
        /// Send a chain connect and clear the source, also invalidating pending extend responses
        /// </summary>
        public static GearChainPoleFrameResult SendChainConnect(GearChainConnectSendCommand command)
        {
            return new GearChainPoleFrameResult(null, true, GearChainPolePreviewCommand.Hidden, null, command, Array.Empty<TooltipLine>(), Array.Empty<ConstructionMaterialShortage>(), null);
        }

        private GearChainPoleFrameResult(IGearChainPoleConnectAreaCollider nextSourcePole, bool invalidatePendingRequest, GearChainPolePreviewCommand preview, GearChainPoleExtendSendCommand? extendSend, GearChainConnectSendCommand? chainConnectSend, IReadOnlyList<TooltipLine> feedbackLines, IReadOnlyList<ConstructionMaterialShortage> materialShortages, LocalizationKey? materialShortageFallbackKey)
        {
            NextSourcePole = nextSourcePole;
            InvalidatePendingRequest = invalidatePendingRequest;
            Preview = preview;
            ExtendSend = extendSend;
            ChainConnectSend = chainConnectSend;
            FeedbackLines = feedbackLines;
            MaterialShortages = materialShortages;
            MaterialShortageFallbackKey = materialShortageFallbackKey;
        }
    }
}
