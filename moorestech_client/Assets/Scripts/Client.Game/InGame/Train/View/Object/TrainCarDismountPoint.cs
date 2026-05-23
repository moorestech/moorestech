using UnityEngine;

namespace Client.Game.InGame.Train.View.Object
{
    // 車両 Prefab に置き、降車地点 Transform を SerializeField で指す。
    // Placed on a train-car Prefab to point at the dismount Transform via a SerializeField.
    public sealed class TrainCarDismountPoint : MonoBehaviour
    {
        public Vector3 Position => transform.position;
    }
}
