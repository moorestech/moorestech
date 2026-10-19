using System;
using System.Collections.Generic;
using Client.Game.InGame.Block;
using UniRx;

namespace Client.Game.InGame.UI.UIState.State.DragDelete.Selection
{
    /// <summary>
    ///     - 論理キーで選択を保持・通知
    ///     - 赤表示と設置演出後の復元
    ///     - 解除待ちの赤戻しを管理
    ///     - Store selection by logical key and notify changes
    ///     - Own red previews and restore them after placement
    ///     - Manage pending preview releases
    /// </summary>
    internal sealed class DeleteSelectedTargetSet
    {
        // 論理削除キーで重複排除する（同一機械の複数メッシュ子などを1件に集約）
        // Dedupe by logical delete key so multiple mesh children of one machine collapse into one
        private readonly Dictionary<object, IDeleteTarget> _targets = new();
        private readonly Dictionary<object, IDisposable> _placeAnimationSubscriptions = new();
        private readonly DeletePreviewResetRecovery _resetRecovery;
        private readonly DeletePreviewReleaseQueue _pendingResets;
        private readonly Subject<int> _selectionChanged = new();

        internal int Count => _targets.Count;
        internal IEnumerable<IDeleteTarget> Values => _targets.Values;
        internal int Revision { get; private set; }
        internal IObservable<int> OnSelectionChanged => _selectionChanged;

        internal DeleteSelectedTargetSet(DeletePreviewResetRecovery resetRecovery)
        {
            _resetRecovery = resetRecovery;
            _pendingResets = new DeletePreviewReleaseQueue(resetRecovery);
        }

        internal bool Contains(object key) => _targets.ContainsKey(key);

        internal void Add(object key, IDeleteTarget target)
        {
            _targets.Add(key, target);
            if (_resetRecovery.CanPreview(target)) target.SetRemovePreviewing();
            // 設置演出で戻された赤を、選択が続く間だけ復元する
            // Restore red after placement animation only while selection still owns the target
            if (target is BlockGameObjectChild child)
            {
                _placeAnimationSubscriptions.Add(key, child.BlockGameObject.OnFinishedPlaceAnimation.Take(1).Subscribe(_ =>
                {
                    if (_targets.TryGetValue(key, out var selected) && ReferenceEquals(selected, target) &&
                        DeleteTargetLiveness.IsAliveOrWarn(target, "[DragDelete] selected target destroyed after placement") && _resetRecovery.CanPreview(target)) target.SetRemovePreviewing();
                }));
            }
            NotifySelectionChanged();
        }

        // 赤表示も解除。未選択ならfalse
        // Also release the red preview; false when not selected
        internal bool TryRemove(IDeleteTarget target)
        {
            if (!RemoveLogicalTarget(target)) return false; // silent-ok: 未選択は変更不要 / 選択件数に反映済み
            _pendingResets.Drain("[DragDelete] selected target was destroyed before deselect");
            return true;
        }

        private bool RemoveLogicalTarget(IDeleteTarget target)
        {
            var key = target.GetDeleteTargetKey();
            if (!_targets.Remove(key, out var removed)) return false; // silent-ok: 未選択は変更不要 / 選択件数に反映済み
            // 表示より先に論理解除と購読解放を終える
            // Finish logical removal and subscription disposal before presentation
            if (_placeAnimationSubscriptions.Remove(key, out var subscription)) subscription.Dispose();
            _pendingResets.Enqueue(new[] { removed });
            NotifySelectionChanged();
            return true;
        }

        internal int RemoveAll(IReadOnlyList<IDeleteTarget> targets)
        {
            var removed = 0;
            foreach (var target in targets)
            {
                if (RemoveLogicalTarget(target)) removed++;
            }
            _pendingResets.Drain("[DragDelete] selected target was destroyed before deselect");
            return removed;
        }

        // 全件を解除待ちへ積んで空にする（赤戻しは DrainReleases で行う）
        // Queue every target for release and empty the set; DrainReleases performs the un-red
        internal void Clear()
        {
            _pendingResets.Enqueue(_targets.Values);
            _targets.Clear();
            foreach (var subscription in _placeAnimationSubscriptions.Values) subscription.Dispose();
            _placeAnimationSubscriptions.Clear();
            NotifySelectionChanged();
        }

        internal void DrainReleases(string destroyedWarning) => _pendingResets.Drain(destroyedWarning);

        private void NotifySelectionChanged()
        {
            Revision++;
            _selectionChanged.OnNext(Revision);
        }
    }
}
