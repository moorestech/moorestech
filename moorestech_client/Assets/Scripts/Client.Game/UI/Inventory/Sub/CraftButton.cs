using UnityEngine;
using UnityEngine.UI;

namespace MainGame.UnityView.UI.Inventory.Sub
{
    public class CraftButton : MonoBehaviour
    {
        [SerializeField] private RectTransform craftButton;
        [SerializeField] private RectMask2D mask;
        private float _percent;

        public float percent
        {
            get => _percent;
            set
            {
                _percent = value;
                UpdateMaskFill();
            }
        }

        private void UpdateMaskFill()
        {
            var maxWidth = craftButton.rect.width;
            var p = maxWidth * (1f - _percent);
            mask.padding = new Vector4(0, 0, p, 0);
            MaskUtilities.NotifyStencilStateChanged(mask);
        }
    }
}
