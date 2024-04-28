using UnityEngine;

namespace Client.Game.InGame.Electric
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