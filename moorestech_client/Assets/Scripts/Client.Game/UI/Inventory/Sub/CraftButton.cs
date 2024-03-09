using UnityEngine;
using UnityEngine.UI;

namespace MainGame.UnityView.UI.Inventory.Sub
{
    public class CraftButton : MonoBehaviour
    {
        [SerializeField] private RectTransform craftButton;
        [SerializeField] private RectMask2D mask;

        public void UpdateMaskFill(float percent)
        {
            var maxWidth = craftButton.rect.width;
            var p = maxWidth * (1f - percent);
            mask.padding = new Vector4(0, 0, p, 0);
        }
    }
}
