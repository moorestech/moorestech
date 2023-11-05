using UnityEngine;

namespace MainGame.Extension
{
    public class EnergizedRangeObject : MonoBehaviour
    {
        public void SetRange(int range)
        {
            var y = transform.localScale.y;
            transform.localScale = new Vector3(range, y, range);
        }
    }
}