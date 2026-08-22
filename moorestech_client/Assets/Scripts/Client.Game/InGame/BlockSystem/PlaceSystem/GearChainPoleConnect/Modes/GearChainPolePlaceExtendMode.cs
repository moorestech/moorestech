using System;
using System.Collections.Generic;
using Client.Game.InGame.BlockSystem.PlaceSystem.Feedback;
using Client.Game.InGame.BlockSystem.PlaceSystem.GearChainPoleConnect.Parts;
using Client.Game.InGame.UI.Tooltip;
using Core.Master;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.GearChainPoleConnect.Modes
{
    /// <summary>
    /// ポールアイテム手持ち時の判断。手持ちポールの孤立設置と、起点からの連続延長設置を決定する。
    /// 純関数であり、入力スナップショットから結果を返すだけで副作用を持たない。
    /// Decision logic while holding a pole item: isolated placement and continuous extension of the holding pole.
    /// A pure function: maps the input snapshot to a result with no side effects.
    /// </summary>
    public static class GearChainPolePlaceExtendMode
    {
        public static GearChainPoleFrameResult Decide(in GearChainPolePlaceExtendInput input)
        {
            // 既存ポール命中中はクリックで延長の起点として選択する
            // While hitting an existing pole, a click selects it as the extension source
            if (input.HitPole != null)
            {
                if (input.Clicked) return GearChainPoleFrameResult.SelectSource(input.HitPole);
                return GearChainPoleFrameResult.Show(input.SourcePole, GearChainPolePreviewCommand.Hidden);
            }

            // 位置なしは非表示。距離外のみ理由を出す
            // Shows nothing without a position; reports a reason only when out of range
            if (!input.HasGhost)
            {
                var noGhostLines = input.GhostTooFar ? new[] { PlacementFeedback.TooFarLine() } : Array.Empty<TooltipLine>();
                return GearChainPoleFrameResult.Show(input.SourcePole, GearChainPolePreviewCommand.Hidden, noGhostLines);
            }

            if (input.SourcePole == null) return DecideIsolatedPlace(input);
            return DecideExtendPlace(input);
        }

        private static GearChainPoleFrameResult DecideIsolatedPlace(in GearChainPolePlaceExtendInput input)
        {
            // 起点なし: 手持ちポールをその場に孤立設置する
            // No source: place the holding pole in isolation
            var placeable = input.GhostGroundClear;
            if (CanSend(input, placeable))
            {
                var canContinue = 0 < input.MaxConnectionCount;
                return GearChainPoleFrameResult.SendExtend(new GearChainPoleExtendSendCommand(null, input.PoleBlockId, input.GhostPlaceInfo, Guid.Empty, canContinue));
            }

            return GearChainPoleFrameResult.Show(null, GearChainPolePreviewCommand.Ghost(placeable), BuildLines(input.GhostGroundClear, GearChainPoleExtendPreviewData.Invalid));
        }

        private static GearChainPoleFrameResult DecideExtendPlace(in GearChainPolePlaceExtendInput input)
        {
            // 起点あり: 手持ちポールを設置し自動選択したチェーンで起点と接続する
            // With a source: place the holding pole and connect with an auto-selected chain
            var placeable = input.ExtendPreview.IsValid && input.ExtendPreview.IsPlaceable && input.GhostGroundClear;
            if (CanSend(input, placeable))
            {
                // 設置直後に接続を1つ使うため、上限1のポールは連続延長を終了する
                // The placed pole uses one connection immediately, so a max-1 pole ends continuous extension
                var canContinue = 1 < input.MaxConnectionCount;
                return GearChainPoleFrameResult.SendExtend(new GearChainPoleExtendSendCommand(input.SourcePolePos, input.PoleBlockId, input.GhostPlaceInfo, input.ConnectToolGuid, canContinue));
            }

            return GearChainPoleFrameResult.Show(input.SourcePole, GearChainPolePreviewCommand.GhostAndLine(placeable, input.SourcePoleCenter, input.GhostCenter), BuildLines(input.GhostGroundClear, input.ExtendPreview));
        }

        // 不可理由を 地形 → チェーン判定 の順で行にする
        // Build the reason lines in order: terrain → chain judgement
        private static IReadOnlyList<TooltipLine> BuildLines(bool ghostGroundClear, GearChainPoleExtendPreviewData extendPreview)
        {
            var lines = new List<TooltipLine>();
            if (!ghostGroundClear) lines.Add(PlacementFeedback.BlockedByTerrainLine());
            if (extendPreview.IsValid) lines.AddRange(GearChainPlacementFailureTooltipKey.BuildFailureLines(extendPreview.IsPlaceable, extendPreview.FailureReason));
            return lines;
        }

        private static bool CanSend(in GearChainPolePlaceExtendInput input, bool placeable)
        {
            // 応答待ち中は誤送信（孤立設置化）を防ぐため送信しない
            // Do not send while awaiting a response to avoid unintended isolated placement
            return placeable && !input.IsAwaitingResponse && input.Clicked;
        }
    }
}
