using System;
using MainGame.Network.Send;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace MainGame.Control.UI.PauseMenu
{
    public class SaveButton : MonoBehaviour
    {
        [SerializeField] private Button saveButton;
        private SendSaveProtocol _sendSaveProtocol;

        [Inject]
        public void Construct(SendSaveProtocol sendSaveProtocol)
        {
            _sendSaveProtocol = sendSaveProtocol;
        }

        private void Start()
        {
            saveButton.onClick.AddListener(_sendSaveProtocol.Send);
        }
    }
}