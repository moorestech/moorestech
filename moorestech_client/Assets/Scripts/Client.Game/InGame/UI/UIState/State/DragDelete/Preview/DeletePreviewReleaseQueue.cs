using System.Collections.Generic;
using UnityEngine;

namespace Client.Game.InGame.UI.UIState.State.DragDelete
{
    // 選択が空になっても未着手の解除を保持する
    // Retain unstarted releases after the logical selection is cleared
    internal sealed class DeletePreviewReleaseQueue
    {
        private readonly Queue<IDeleteTarget> _pending = new();
        private readonly DeletePreviewResetRecovery _recovery;
        internal DeletePreviewReleaseQueue(DeletePreviewResetRecovery recovery) { _recovery = recovery; }
        internal void Enqueue(IEnumerable<IDeleteTarget> targets)
        {
            foreach (var target in targets) _pending.Enqueue(target);
        }
        internal void Drain(string destroyedWarning)
        {
            while (_pending.Count > 0)
            {
                var target = _pending.Dequeue();
                var completed = false;
                // 例外を隠さず残りの表示を解放し、次の離脱操作を守る
                // Release remaining previews without hiding exceptions so the next exit can proceed
                try
                {
                    _recovery.Reset(target, destroyedWarning);
                    completed = true;
                }
                finally
                {
                    if (!completed)
                    {
                        Debug.LogWarning($"[DragDelete] preview reset failed for {target.GetType().Name}; releasing remaining targets");
                        Drain(destroyedWarning);
                    }
                }
            }
        }
    }
}
