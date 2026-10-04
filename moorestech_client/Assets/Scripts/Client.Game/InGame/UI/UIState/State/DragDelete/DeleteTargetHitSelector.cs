using System.Collections.Generic;
using Client.Game.InGame.UI.UIState.State;

namespace Client.Game.InGame.UI.UIState.State.DragDelete
{
    /// <summary>
    ///     照準レイ上のヒット1件（距離と、解決できた削除対象。非対象の遮蔽物はnull）
    ///     One aim-ray hit (distance and the resolved delete target; null for a non-target occluder)
    /// </summary>
    public readonly struct DeleteTargetHit
    {
        public readonly float Distance;
        public readonly IDeleteTarget Target;

        public DeleteTargetHit(float distance, IDeleteTarget target)
        {
            Distance = distance;
            Target = target;
        }
    }

    /// <summary>
    ///     ヒット列から削除対象を選ぶ。最前面条件は最前面、カテゴリー条件はそのカテゴリーの最前面
    ///     Picks a delete target from hits: frontmost under the frontmost filter, frontmost of the category under a category filter
    /// </summary>
    public static class DeleteTargetHitSelector
    {
        public static DeleteAimResult Select(IReadOnlyList<DeleteTargetHit> hits, DeleteAimFilter filter)
        {
            if (hits.Count == 0) return DeleteAimResult.Missed(DeleteAimOutcome.NothingHit);

            var bestDistance = float.MaxValue;
            IDeleteTarget best = null;
            var hasBest = false;
            var hasTarget = false;
            foreach (var hit in hits)
            {
                if (hit.Target != null) hasTarget = true;
                if (bestDistance <= hit.Distance) continue;

                // カテゴリー条件では非対象・別カテゴリーを貫通する
                // Under a category filter, pass through non-targets and other categories
                if (filter.IsCategoryRequired && (hit.Target == null || !filter.Accepts(hit.Target))) continue;

                bestDistance = hit.Distance;
                best = hit.Target;
                hasBest = true;
            }

            // 別カテゴリーの対象だけの場合と非対象だけの場合を区別する
            // Distinguish off-category targets from hits containing only non-targets
            if (!hasBest) return DeleteAimResult.Missed(hasTarget ? DeleteAimOutcome.NoTargetOfCategory : DeleteAimOutcome.OccludedByNonTarget);
            if (best == null) return DeleteAimResult.Missed(DeleteAimOutcome.OccludedByNonTarget);
            return DeleteAimResult.Found(best);
        }
    }
}
