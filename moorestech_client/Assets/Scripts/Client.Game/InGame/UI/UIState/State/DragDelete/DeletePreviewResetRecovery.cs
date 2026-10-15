using System.Collections.Generic;
using UnityEngine;

namespace Client.Game.InGame.UI.UIState.State.DragDelete
{
    // ホバーと選択で共有する戻し処理の回復状態
    // Reset recovery state shared by hover and selection previews
    public class DeletePreviewResetRecovery
    {
        private readonly HashSet<IDeleteTarget> _incompleteResets = new();
        private readonly HashSet<IDeleteTarget> _warnedIncompleteResets = new();

        internal bool CanPreview(IDeleteTarget target)
        {
            if (!_incompleteResets.Contains(target)) return true;

            // 抑止の初回だけ理由を残してログ連打を防ぐ
            // Report the first suppression without repeating the warning each frame
            if (_warnedIncompleteResets.Add(target))
                Debug.LogWarning("[DragDelete] delete preview suppressed because the target's previous reset did not complete");
            // silent-ok: 失敗対象の再保持とReset再実行を避ける / 初回警告と元の例外をUnity Consoleで開発者が確認する
            return false;
        }

        internal void Reset(IDeleteTarget target, string destroyedWarning)
        {
            if (!DeleteTargetLiveness.IsAliveOrWarn(target, destroyedWarning)) return;
            if (!CanPreview(target)) return; // silent-ok: 再実行で同じ例外を繰り返さない / CanPreviewが初回警告を記録する

            // 成功時だけ解除し、例外は呼び出し元へそのまま渡す
            // Clear the mark only on success and let exceptions reach the caller
            _incompleteResets.Add(target);
            target.ResetMaterial();
            _incompleteResets.Remove(target);
        }
    }
}
