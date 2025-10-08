using UnityEngine;

namespace Client.Game.InGame.Electric
{
    public class EnergizedRangeObject : MonoBehaviour
    {
        public void SetRange(int horizontalRange, int heightRange)
        {
            horizontalRange = Mathf.Max(horizontalRange, 1);
            heightRange = Mathf.Max(heightRange, 1);
            transform.localScale = new Vector3(horizontalRange, heightRange, horizontalRange);
        }
    }
}
