using UnityEngine;

namespace Client.Game.InGame.Train.View.Object
{
    public sealed class TrainCarVisualPart : MonoBehaviour
    {
        // Prefab上の表示part順とauthored長を保持する
        // Store the visual-part order and authored length on the Prefab
        [SerializeField] private int order;
        [SerializeField] private float lengthMeters;
        [SerializeField] private Transform poseTarget;

        public int GetOrder()
        {
            return order;
        }

        public float GetLengthMeters()
        {
            return lengthMeters;
        }

        public Transform GetPoseTarget()
        {
            // 明示targetがなければこのcomponentのTransformを動かす
            // Move this component's Transform when no explicit target is assigned
            return poseTarget != null ? poseTarget : transform;
        }
    }
}
