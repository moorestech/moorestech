using System.Collections.Generic;
using Client.Common;
using Client.Game.InGame.Control;
using Client.Game.InGame.Player;
using UnityEngine;

namespace Client.Game.InGame.Interact.Selection
{
    /// <summary>
    ///     インタラクト対象を常に1件だけ選ぶ。照準レイのヒットを優先し、無ければ半径2m内で視線角度が最小のもの（ADR 0046）
    ///     Picks exactly one interactable: the aim-ray hit first, else the smallest view angle within 2m (ADR 0046)
    /// </summary>
    public class InteractTargetSelector : IInteractTargetSelector
    {
        public const float InteractDistance = 2f;

        private const int InitialOverlapBufferSize = 64;

        private static readonly int InteractLayerMask = LayerConst.BlockOnlyLayerMask | LayerConst.MapObjectOnlyLayerMask;

        private Collider[] _overlapBuffer = new Collider[InitialOverlapBufferSize];

        private readonly List<NearbyCandidate> _candidates = new();

        // 走査結果の器は使い回す。返した値が指すのは常に直近のScan1回分で、寿命はそのフレーム内
        // The result holder is reused; a returned value always shows the latest single Scan and lives only for that frame
        private readonly InteractSelection _selection = new();

        // 照準ヒットと近傍候補を1回で集める。毎フレームの物理問い合わせはこの1回だけ
        // Collects the aim hit and the nearby candidates once, which is the only physics query of the frame
        public IInteractSelection Scan()
        {
            _candidates.Clear();

            var camera = Camera.main;
            if (camera == null || UiPointerHitTest.IsPointerOverAnyUi())
            {
                _selection.SetEmptyScanResult();
                return _selection;
            }

            var playerPosition = PlayerSystemContainer.Instance.PlayerObjectController.Position;
            var viewForward = camera.transform.forward;

            // カメラ後退分を足した距離まで撃ち、到達判定はプレイヤーから測る
            // The ray spans the camera pull-back plus the reach, while the reach itself is measured from the player
            var rayDistance = Vector3.Distance(camera.transform.position, playerPosition) + InteractDistance;
            if (BlockClickDetectUtil.TryGetFrontmostSolidHit(InteractLayerMask, rayDistance, out var hit) &&
                Vector3.Distance(playerPosition, hit.point) <= InteractDistance)
            {
                // 手の届く実体は対象外でもそこで確定させる。近傍へ落とすと遮蔽物越しに機械を開ける
                // A solid within reach settles the frame even when it is no target; falling through would open a machine through the wall
                IInteractable aimedTarget = null;
                if (InteractableResolver.TryResolve(hit.collider, playerPosition, out var interactable, out _)) aimedTarget = interactable;

                _selection.SetScanResult(aimedTarget, _candidates, viewForward, playerPosition);
                return _selection;
            }

            var hitCount = OverlapNearby(playerPosition);
            for (var index = 0; index < hitCount; index++)
            {
                if (!InteractableResolver.TryResolve(_overlapBuffer[index], playerPosition, out var candidate, out var candidatePoint)) continue;

                // 同一対象の複数コライダは1件に畳む
                // Extra colliders of one target collapse into a single entry
                if (!ContainsCandidate(candidate)) _candidates.Add(new NearbyCandidate(candidate, candidatePoint));
            }

            _selection.SetScanResult(null, _candidates, viewForward, playerPosition);
            return _selection;

            #region Internal

            // 飽和したまま返すと取りこぼした候補次第で選定が変わるため、バッファを倍にして採り直す
            // A saturated buffer would make the pick depend on which candidates were dropped, so it is doubled and re-queried
            int OverlapNearby(Vector3 center)
            {
                while (true)
                {
                    var count = Physics.OverlapSphereNonAlloc(center, InteractDistance, _overlapBuffer, InteractLayerMask);
                    if (count < _overlapBuffer.Length) return count;

                    _overlapBuffer = new Collider[_overlapBuffer.Length * 2];
                }
            }

            bool ContainsCandidate(IInteractable interactable)
            {
                foreach (var candidate in _candidates)
                    if (ReferenceEquals(candidate.Interactable, interactable))
                        return true;

                return false;
            }

            #endregion
        }
    }
}
