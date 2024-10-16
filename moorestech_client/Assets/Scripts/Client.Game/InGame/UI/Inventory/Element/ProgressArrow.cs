using UnityEngine;
using UnityEngine.UI;

namespace Client.Game.InGame.UI.Inventory.Element
{
    public class ProgressArrow : MonoBehaviour
    {
        [SerializeField] private float minPadding;
        [SerializeField] private float maxPadding;
        [SerializeField] private RectMask2D rectMask2D;
        
        public void SetProgress(float value)
        {
            var padding = Mathf.Lerp(minPadding, maxPadding, value);
            rectMask2D.padding = new Vector4(0, 0, padding, 0);
        }
    }
}