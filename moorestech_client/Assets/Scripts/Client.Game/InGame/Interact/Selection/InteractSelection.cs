using System.Collections.Generic;
using Client.Game.InGame.Interact.Tap;
using Client.Input;
using UnityEngine;

namespace Client.Game.InGame.Interact.Selection
{
    /// <summary>
    ///     1フレーム分の走査結果を保持し、主対象もキー別の候補も同じ集合の上で答える
    ///     Holds one frame's scan result so the primary target and the per-key candidates answer on that same set
    /// </summary>
    internal sealed class InteractSelection : IInteractSelection
    {
        private readonly List<NearbyCandidate> _candidates = new();

        private IInteractable _aimedTarget;
        private Vector3 _viewForward;
        private Vector3 _playerPosition;

        public IInteractable Primary { get; private set; }

        // カメラもUIも無いフレームは候補を持たない
        // A frame without a camera, or spent over the UI, carries no candidates
        public void SetEmptyScanResult()
        {
            _candidates.Clear();
            _aimedTarget = null;
            Primary = null;
        }

        // 走査した瞬間の視線と候補を丸ごと入れ替える。主対象はここで確定する
        // Replaces the view and the candidates with the ones captured by the scan, settling the primary target here
        public void SetScanResult(IInteractable aimedTarget, List<NearbyCandidate> candidates, Vector3 viewForward, Vector3 playerPosition)
        {
            _candidates.Clear();
            _candidates.AddRange(candidates);
            _aimedTarget = aimedTarget;
            _viewForward = viewForward;
            _playerPosition = playerPosition;

            Primary = aimedTarget != null ? aimedTarget : SelectBestByViewAngle(null);
        }

        // 主対象が応じないキーを走査済みの候補で捌く
        // Answers a key the primary target does not offer, on the scanned candidates
        public IInteractable SelectRespondingTo(InputKey key)
        {
            if (_aimedTarget != null && RespondsTo(_aimedTarget, key)) return _aimedTarget;

            return SelectBestByViewAngle(key);
        }

        public void CollectCandidateKeys(List<InputKey> keys)
        {
            keys.Clear();

            if (_aimedTarget != null) AddKeys(_aimedTarget);
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

        // 視線角度が最小の候補を選ぶ。keyで対象を絞る
        // Picks the smallest view angle; a key narrows it to the candidates answering it
        private IInteractable SelectBestByViewAngle(InputKey key)
        {
            IInteractable best = null;
            var bestAngle = float.PositiveInfinity;
            var bestSqrDistance = float.PositiveInfinity;

            foreach (var candidate in _candidates)
            {
                if (key != null && !RespondsTo(candidate.Interactable, key)) continue;

                var toCandidate = candidate.Point - _playerPosition;
                var angle = Vector3.Angle(_viewForward, toCandidate);
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
