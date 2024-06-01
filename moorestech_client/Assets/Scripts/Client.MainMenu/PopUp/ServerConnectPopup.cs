using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Client.MainMenu.PopUp
{
    public class ServerConnectPopup : MonoBehaviour
    {
        [SerializeField] private TMP_Text logText;
        [SerializeField] private Button closeButton;

        private void Start()
        {
            closeButton.onClick.AddListener(() =>
                gameObject.SetActive(false)
            );
        }

        public void SetText(string text)
        {
            gameObject.SetActive(true);
            logText.text = text;
        }
    }
}