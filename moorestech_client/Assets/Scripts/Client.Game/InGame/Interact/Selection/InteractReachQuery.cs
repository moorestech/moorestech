using UnityEngine;

namespace Client.Game.InGame.Interact.Selection
{
    /// <summary>
    ///     開いた対象がまだ手の届く範囲にあるか。候補選定の近傍探索と同じ問い合わせで答える
    ///     Whether an opened target is still within reach, answered by the same nearby query the selection uses
    /// </summary>
    public class InteractReachQuery
    {
        private Collider[] _overlapBuffer = new Collider[InteractOverlap.InitialBufferSize];

        // 開けた位置なら近傍探索に必ず掛かるので、開いた直後に届かない判定にはならない
        // Any position the target was opened from is caught by the nearby query, so it never reads out of reach right after opening
        public bool IsWithinReach(IInteractable target, Vector3 playerPosition)
        {
            var hitCount = InteractOverlap.OverlapNearby(playerPosition, ref _overlapBuffer);
            for (var index = 0; index < hitCount; index++)
            {
                if (!InteractableResolver.TryResolve(_overlapBuffer[index], playerPosition, out var resolved, out _)) continue;
                if (ReferenceEquals(resolved, target)) return true;
            }

            return false;
        }
    }
}
