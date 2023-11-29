using UnityEngine;
using UnityEngine.UI;

namespace MainGame.UnityView.UI.UIObjects
{
    public class UIBuilderProgressArrowObject : MonoBehaviour
    {
        [SerializeField] private Image arrowBlack;

        public void SetFillAmount(float amount)
        {
            arrowBlack.fillAmount = amount;
        }
    }
}