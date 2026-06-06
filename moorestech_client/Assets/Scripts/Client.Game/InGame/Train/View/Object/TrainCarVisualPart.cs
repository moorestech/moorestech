using UnityEngine;

namespace Client.Game.InGame.Train.View.Object
{
    public sealed class TrainCarVisualPart : MonoBehaviour
    {
        // Prefab 上の表示 part 順序と authored 長を保持する
        // Store the visual-part order and authored length on the Prefab
        [SerializeField] private int order;
        [SerializeField] private int lengthMeters;
        [SerializeField] private Transform poseTarget;

        public int GetOrder()
        {
            return order;
        }

        public int GetLengthMeters()
        {
            return lengthMeters;
        }

        public Transform GetPoseTarget()
        {
            // 明示 target がなければ component の Transform を動かす
            // Move this component's Transform when no explicit target is assigned
            return poseTarget != null ? poseTarget : transform;
        }
    }
}
