using UnityEngine;
using UnityEngine.UI;

namespace MainGame.UnityView.UI.Builder.Unity
{
    public class ProgressArrowObject : MonoBehaviour
    {
        [SerializeField] private Image arrowBlack;

        public void SetFillAmount(float amount)
        {
            arrowBlack.fillAmount = amount;
        }
    }
}