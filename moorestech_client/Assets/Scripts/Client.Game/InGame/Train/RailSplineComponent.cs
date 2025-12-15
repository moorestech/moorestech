using UnityEngine;

namespace Client.Game.InGame.Train
{
    /// <summary>
    ///     レール可視化用のプレースホルダーComponent
    /// </summary>
    public sealed class RailSplineComponent : MonoBehaviour
    {
        public int FromNodeId { get; private set; }
        public int ToNodeId { get; private set; }

        public void Initialize(int fromNodeId, int toNodeId)
        {
            FromNodeId = fromNodeId;
            ToNodeId = toNodeId;
        }
    }
}
