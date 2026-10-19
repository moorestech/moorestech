using System;
using System.Collections.Generic;
using Client.Game.InGame.Block;
using UniRx;

namespace Client.Game.InGame.UI.UIState.State.DragDelete.Selection
{
    /// <summary>
    ///     選択済みの削除対象を論理キーで保持し、赤表示・設置演出後の赤の復元・解除待ちの赤戻し・変更通知を持つ
    ///     Holds selected delete targets by logical key, owning their red preview, its restore after placement, pending un-red releases and change notification
    /// </summary>
    internal sealed class DeleteSelectedTargetSet
    {
        // 論理削除キーで重複排除する（同一機械の複数メッシュ子などを1件に集約）
        // Dedupe by logical delete key so multiple mesh children of one machine collapse into one
        private readonly Dictionary<object, IDeleteTarget> _targets = new();
        private readonly CompositeDisposable _placeAnimationSubscriptions = new();
        private readonly DeletePreviewResetRecovery _resetRecovery;
        private readonly DeletePreviewReleaseQueue _pendingResets;
        private readonly Subject<int> _changed = new();

        internal int Count => _targets.Count;
        internal IEnumerable<IDeleteTarget> Values => _targets.Values;
        internal int Revision { get; private set; }
        internal IObservable<int> OnChanged => _changed;

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
                child.BlockGameObject.OnFinishedPlaceAnimation.Take(1).Subscribe(_ =>
                {
                    if (_targets.TryGetValue(key, out var selected) && ReferenceEquals(selected, target) &&
                        DeleteTargetLiveness.IsAliveOrWarn(target, "[DragDelete] selected target destroyed after placement") && _resetRecovery.CanPreview(target)) target.SetRemovePreviewing();
                }).AddTo(_placeAnimationSubscriptions);
            }
            NotifyChanged();
        }

        // 選択済みなら外して赤を戻す。未選択は外す物が無いので false
        // Remove and un-red a selected target; false when it was not selected
        internal bool TryRemove(IDeleteTarget target)
        {
            if (!_targets.Remove(target.GetDeleteTargetKey(), out var removed)) return false; // silent-ok: 未選択の対象は外す物が無い / 選択件数のツールチップが変わらないことで見える
            // 論理状態と通知を先に済ませ、赤戻しが失敗しても選択から外れた状態を保つ
            // Update logical state and notify first so a failing un-red still leaves the target deselected
            _pendingResets.Enqueue(new[] { removed });
            NotifyChanged();
            _pendingResets.Drain("[DragDelete] selected target was destroyed before deselect");
            return true;
        }

        internal int RemoveAll(IReadOnlyList<IDeleteTarget> targets)
        {
            var removed = 0;
            foreach (var target in targets)
            {
                if (TryRemove(target)) removed++;
            }
            return removed;
        }

        // 全件を解除待ちへ積んで空にする（赤戻しは DrainReleases で行う）
        // Queue every target for release and empty the set; DrainReleases performs the un-red
        internal void Clear()
        {
            _pendingResets.Enqueue(_targets.Values);
            _targets.Clear();
            _placeAnimationSubscriptions.Clear();
            NotifyChanged();
        }

        internal void DrainReleases(string destroyedWarning) => _pendingResets.Drain(destroyedWarning);

        private void NotifyChanged()
        {
            Revision++;
            _changed.OnNext(Revision);
        }
    }
}
