using UnityEngine;
using UnityEngine.UI;

namespace Client.Game.InGame.UI.Inventory.Common
{
    public class ProgressArrowView : MonoBehaviour
    {
        [SerializeField] private Slider slider;
        
        public void SetProgress(float value)
        {
            slider.value = value;
        }
    }
}