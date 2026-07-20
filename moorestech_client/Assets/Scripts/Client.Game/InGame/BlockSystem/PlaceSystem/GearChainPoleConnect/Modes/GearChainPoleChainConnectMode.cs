using Client.Game.InGame.BlockSystem.PlaceSystem.GearChainPoleConnect.Parts;

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
                if (input.SourcePole != null && input.HasCursorPoint) return GearChainPoleFrameResult.Show(input.SourcePole, GearChainPolePreviewCommand.Line(input.SourcePoleCenter, input.CursorPoint, false));
                return GearChainPoleFrameResult.Show(input.SourcePole, GearChainPolePreviewCommand.Hidden);
            }

            // 起点未選択ならクリックで起点を選択する
            // Select the source pole by click when none is selected
            if (input.SourcePole == null)
            {
                if (input.Clicked) return GearChainPoleFrameResult.SelectSource(input.HitPole);
                return GearChainPoleFrameResult.Show(null, GearChainPolePreviewCommand.Hidden);
            }

            // 起点自身への接続は無効
            // Connecting the source to itself is invalid
            if (input.SourcePolePos == input.HitPolePos) return GearChainPoleFrameResult.Show(input.SourcePole, GearChainPolePreviewCommand.Hidden);

            // 起点情報が解決できない場合はクリックで起点を選び直せるようにする（消失ポール対策）
            // Allow re-selecting the source by click when it cannot be resolved (handles removed poles)
            if (!input.PoleToPolePreview.IsValid)
            {
                if (input.Clicked) return GearChainPoleFrameResult.SelectSource(input.HitPole);
                return GearChainPoleFrameResult.Show(input.SourcePole, GearChainPolePreviewCommand.Hidden);
            }

            // 接続可能な状態でクリックされたら接続プロトコルを送信する
            // Send the connect protocol when clicked in a connectable state
            if (input.PoleToPolePreview.IsPlaceable && input.Clicked) return GearChainPoleFrameResult.SendChainConnect(new GearChainConnectSendCommand(input.SourcePolePos, input.HitPolePos, input.ConnectToolGuid));

            return GearChainPoleFrameResult.Show(input.SourcePole, GearChainPolePreviewCommand.Line(input.PoleToPolePreview.StartPoint, input.PoleToPolePreview.EndPoint, input.PoleToPolePreview.IsPlaceable));
        }
    }
}
