using System.Collections.Generic;
using UnityEngine;

namespace Client.Game.InGame.UI.UIState.State.DragDelete
{
    // 単体ホバーの赤プレビュー対象を1件持つ。戻しが例外でも毎フレーム再発しないよう参照を先に手放す
    // Holds the single-hover preview target; drops the reference before resetting so a failing reset never repeats every frame
    public class SingleHoverDeletePreview
    {
        // 正常完了しなかった戻し対象と警告済み対象を保持する
        // Retain incomplete resets and deduplicate their suppression warnings
        private readonly HashSet<IDeleteTarget> _incompleteResets = new();
        private readonly HashSet<IDeleteTarget> _warnedIncompleteResets = new();

        public IDeleteTarget Current { get; private set; }

        public void Hover(IDeleteTarget hovered)
        {
            if (Current == hovered) return;

            // 旧対象を戻してから新対象を赤くする
            // Reset the previous target, then paint the new one red
            Release();
            if (_incompleteResets.Contains(hovered))
            {
                if (_warnedIncompleteResets.Add(hovered))
                    Debug.LogWarning("[DragDelete] hover preview suppressed because the target's previous reset did not complete");
                // silent-ok: 失敗対象の再保持を避けるため継続 / 初回警告と元の例外をUnity Consoleで開発者が確認する
                return;
            }
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
            // 呼び出し前に未完了を記録し、成功した場合だけ解除する
            // Mark reset incomplete before calling it and clear the mark only on success
            _incompleteResets.Add(previous);
            previous.ResetMaterial();
            _incompleteResets.Remove(previous);
        }
    }
}
