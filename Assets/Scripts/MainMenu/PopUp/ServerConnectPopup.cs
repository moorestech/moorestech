using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MainMenu.PopUp
{
    public class ServerConnectPopup : MonoBehaviour
    {
        [SerializeField] private TextMeshPro test;
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
            test.text = text;
        }
    }
}