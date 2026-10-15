using Client.Game.InGame.UI.UIState.State;

namespace Client.Game.InGame.UI.UIState.State.DragDelete
{
    // 単体ホバーの赤プレビュー対象を1件持つ。戻しが例外でも毎フレーム再発しないよう参照を先に手放す
    // Holds the single-hover preview target; drops the reference before resetting so a failing reset never repeats every frame
    public class SingleHoverDeletePreview
    {
        public IDeleteTarget Current { get; private set; }

        public void Hover(IDeleteTarget hovered)
        {
            if (Current == hovered) return;

            // 旧対象を戻してから新対象を赤くする
            // Reset the previous target, then paint the new one red
            Release();
            Current = hovered;
            hovered.SetRemovePreviewing();
        }

        public void Release()
        {
            if (Current == null) return;

            // 戻し処理の失敗で参照が残ると次フレームも同じ例外になるため先に外す
            // Detach first: a reference left behind by a failing reset would throw again next frame
            var previous = Current;
            Current = null;
            if (!DeleteTargetLiveness.IsAliveOrWarn(previous, "[DragDelete] hovered target was destroyed before reset")) return;
            previous.ResetMaterial();
        }
    }
}
