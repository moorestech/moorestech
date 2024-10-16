using UnityEngine;
using UnityEngine.UI;

namespace Client.Game.InGame.UI.Inventory.Element
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