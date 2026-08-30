using UnityEngine;

namespace Client.Game.InGame.Interact
{
    /// <summary>
    ///     近傍候補と、距離・角度を測る代表点
    ///     A nearby candidate together with the point distance and angle are measured at
    /// </summary>
    internal readonly struct NearbyCandidate
    {
        public IInteractable Interactable { get; }
        public Vector3 Point { get; }

        public NearbyCandidate(IInteractable interactable, Vector3 point)
        {
            Interactable = interactable;
            Point = point;
        }
    }
}
