using System;
using Client.Game.InGame.BlockSystem.PlaceSystem.GearChainPoleConnect.Parts;
using Client.Game.InGame.BlockSystem.PlaceSystem.Util;
using Client.Game.InGame.UI.Tooltip;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.GearChainPoleConnect.Modes
{
    /// <summary>
    /// チェーンアイテム手持ち時の判断。既存の設置済みポール同士の接続のみを決定し、ポールの新規設置はしない。
    /// 純関数であり、入力スナップショットから結果を返すだけで副作用を持たない。
    /// Decision logic while holding a chain item: only connects existing placed poles and never places new poles.
    /// A pure function: maps the input snapshot to a result with no side effects.
    /// </summary>
    public static class GearChainPoleChainConnectMode
    {
        public static GearChainPoleFrameResult Decide(in GearChainPoleChainConnectInput input)
        {
            // ポール非命中: 起点があればカーソルへ設置不可の赤線のみ表示する
            // No pole hit: show only the unplaceable red line from the source to the cursor
            if (input.HitPole == null)
            {
                if (input.SourcePole != null && input.HasCursorPoint) return ShowOnly(input.SourcePole, GearChainPolePreviewCommand.Line(input.SourcePoleCenter, input.CursorPoint, false));
                return ShowOnly(input.SourcePole, GearChainPolePreviewCommand.Hidden);
            }

            // 起点未選択ならクリックで起点を選択する
            // Select the source pole by click when none is selected
            if (input.SourcePole == null)
            {
                if (input.Clicked) return GearChainPoleFrameResult.SelectSource(input.HitPole);
                return ShowOnly(null, GearChainPolePreviewCommand.Hidden);
            }

            // 起点自身への接続は無効
            // Connecting the source to itself is invalid
            if (input.SourcePolePos == input.HitPolePos) return ShowOnly(input.SourcePole, GearChainPolePreviewCommand.Hidden);

            // 起点情報が解決できない場合はクリックで起点を選び直せるようにする（消失ポール対策）
            // Allow re-selecting the source by click when it cannot be resolved (handles removed poles)
            if (!input.PoleToPolePreview.IsValid)
            {
                if (input.Clicked) return GearChainPoleFrameResult.SelectSource(input.HitPole);
                return ShowOnly(input.SourcePole, GearChainPolePreviewCommand.Hidden);
            }

            // 接続可能な状態でクリックされたら接続プロトコルを送信する
            // Send the connect protocol when clicked in a connectable state
            if (input.PoleToPolePreview.IsPlaceable && input.Clicked) return GearChainPoleFrameResult.SendChainConnect(new GearChainConnectSendCommand(input.SourcePolePos, input.HitPolePos, input.ConnectToolGuid));

            // 接続不可なら判定の理由を行にする。素材不足は行にせず判定ごと渡し、不足枠を開けるかはファクトリが決める
            // Turn the judgement reason into a line when the connection is not possible; a material shortage travels as the judgement itself and the factory decides whether the shortage slot opens
            var preview = GearChainPolePreviewCommand.Line(input.PoleToPolePreview.StartPoint, input.PoleToPolePreview.EndPoint, input.PoleToPolePreview.IsPlaceable);
            var lines = GearChainPlacementFailureTooltipKey.BuildFailureLines(input.PoleToPolePreview.IsPlaceable, input.PoleToPolePreview.FailureReason);

            // このモードはポールを設置しないのでゴースト建設コストの枠は常に空
            // This mode never places a pole, so the ghost construction cost slot is always empty
            return GearChainPoleFrameResult.Show(input.SourcePole, preview, lines, input.PoleToPolePreview, Array.Empty<ConstructionMaterialShortage>());
        }

        // チェーン判定を伴わない表示。理由行も不足も無い
        // A display without any chain judgement: no reason lines and no shortages
        private static GearChainPoleFrameResult ShowOnly(IGearChainPoleConnectAreaCollider sourcePole, GearChainPolePreviewCommand preview)
        {
            return GearChainPoleFrameResult.Show(sourcePole, preview, Array.Empty<TooltipLine>(), GearChainPoleExtendPreviewData.Invalid, Array.Empty<ConstructionMaterialShortage>());
        }
    }
}
