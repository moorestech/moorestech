using System.Collections.Generic;
using Client.Common;
using Client.Game.InGame.Control;
using Client.Game.InGame.Interact.Tap;
using Client.Game.InGame.Player;
using Client.Input;
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

        // 直近のSelect()が集めた候補。キー収集とキー別選定は同じ走査結果の上で答える（毎フレームの物理問い合わせは1回だけ）
        // Candidates from the latest Select(); key collection and per-key selection answer on that same scan, so physics is queried once per frame
        private bool _hasScanned;
        private IInteractable _scannedAimedTarget;

        public IInteractable Select()
        {
            if (!Scan(out var aimedTarget)) return null;
            if (aimedTarget != null) return aimedTarget;

            return SelectBestByViewAngle(null);
        }

        // 主対象が応じないキーを直近の走査結果で捌く
        // Answers a key the primary target does not offer, on the latest scan's candidates
        public IInteractable SelectRespondingTo(InputKey key)
        {
            if (!_hasScanned) return null;
            if (_scannedAimedTarget != null && RespondsTo(_scannedAimedTarget, key)) return _scannedAimedTarget;

            return SelectBestByViewAngle(key);
        }

        public void CollectCandidateKeys(List<InputKey> keys)
        {
            keys.Clear();
            if (!_hasScanned) return;

            if (_scannedAimedTarget != null) AddKeys(_scannedAimedTarget);
            foreach (var candidate in _candidates) AddKeys(candidate.Interactable);

            #region Internal

            void AddKeys(IInteractable interactable)
            {
                if (interactable is not ITapInteractable tapInteractable) return;

                foreach (var action in tapInteractable.Actions)
                    if (!keys.Contains(action.Key))
                        keys.Add(action.Key);
            }

            #endregion
        }

        // 照準ヒットと近傍候補を1回で集める
        // Collects the aim hit and the nearby candidates once so every query sees the same set
        private bool Scan(out IInteractable aimedTarget)
        {
            aimedTarget = null;
            _scannedAimedTarget = null;
            _hasScanned = false;
            _candidates.Clear();

            var camera = Camera.main;
            if (camera == null) return false;
            if (UiPointerHitTest.IsPointerOverAnyUi()) return false;

            _hasScanned = true;

            var playerPosition = PlayerSystemContainer.Instance.PlayerObjectController.Position;

            // カメラ後退分を足した距離まで撃ち、到達判定はプレイヤーから測る
            // The ray spans the camera pull-back plus the reach, while the reach itself is measured from the player
            var rayDistance = Vector3.Distance(camera.transform.position, playerPosition) + InteractDistance;
            if (BlockClickDetectUtil.TryGetFrontmostSolidHit(InteractLayerMask, rayDistance, out var hit) &&
                Vector3.Distance(playerPosition, hit.point) <= InteractDistance)
            {
                // 手の届く実体は対象外でもそこで確定させる。近傍へ落とすと遮蔽物越しに機械を開ける
                // A solid within reach settles the frame even when it is no target; falling through would open a machine through the wall
                if (InteractableResolver.TryResolve(hit.collider, playerPosition, out var interactable, out _)) _scannedAimedTarget = interactable;

                aimedTarget = _scannedAimedTarget;
                return true;
            }

            var hitCount = OverlapNearby(playerPosition);
            for (var index = 0; index < hitCount; index++)
            {
                if (!InteractableResolver.TryResolve(_overlapBuffer[index], playerPosition, out var candidate, out var candidatePoint)) continue;

                // 同一対象の複数コライダは1件に畳む
                // Extra colliders of one target collapse into a single entry
                if (!ContainsCandidate(candidate)) _candidates.Add(new NearbyCandidate(candidate, candidatePoint));
            }

            return true;

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

        // 視線角度が最小の候補を選ぶ。keyで対象を絞る
        // Picks the smallest view angle; a key narrows it to the candidates answering it
        private IInteractable SelectBestByViewAngle(InputKey key)
        {
            var camera = Camera.main;
            var forward = camera.transform.forward;
            var playerPosition = PlayerSystemContainer.Instance.PlayerObjectController.Position;

            IInteractable best = null;
            var bestAngle = float.PositiveInfinity;
            var bestSqrDistance = float.PositiveInfinity;

            foreach (var candidate in _candidates)
            {
                if (key != null && !RespondsTo(candidate.Interactable, key)) continue;

                var toCandidate = candidate.Point - playerPosition;
                var angle = Vector3.Angle(forward, toCandidate);
                var sqrDistance = toCandidate.sqrMagnitude;

                // 角度が小さい方を優先し、同角度なら近い方
                // Prefer the smaller angle and the closer one on a tie
                var isBetter = angle < bestAngle || (Mathf.Approximately(angle, bestAngle) && sqrDistance < bestSqrDistance);
                if (!isBetter) continue;

                best = candidate.Interactable;
                bestAngle = angle;
                bestSqrDistance = sqrDistance;
            }

            return best;
        }

        private static bool RespondsTo(IInteractable interactable, InputKey key)
        {
            if (interactable is not ITapInteractable tapInteractable) return false;

            foreach (var action in tapInteractable.Actions)
                if (action.Key == key)
                    return true;

            return false;
        }
    }
}
