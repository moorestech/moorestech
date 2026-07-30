using Client.MainMenu.PopUp;
using UnityEngine;
using UnityEngine.UI;

namespace Client.MainMenu
{
    public class ResetAllDataButton : MonoBehaviour
    {
        [SerializeField] private Button resetAllDataButton;
        [SerializeField] private ResetAllDataConfirmPopup confirmPopup;

        private void Start()
        {
            resetAllDataButton.onClick.AddListener(confirmPopup.Open);
        }
    }
}
