using UnityEngine;
using UnityEngine.UI;

namespace Client.Game.InGame.UI.Inventory.Element
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