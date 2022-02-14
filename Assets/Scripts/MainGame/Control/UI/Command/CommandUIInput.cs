using System;
using MainGame.Network.Send;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace MainGame.Control.UI.Command
{
    public class CommandUIInput: MonoBehaviour
    {
        [SerializeField] private InputField commandInputField;
        [SerializeField] private Button submitButton;

        private SendCommandProtocol _sendCommandProtocol;


        [Inject]
        public void Construct(SendCommandProtocol sendCommandProtocol)
        {
            _sendCommandProtocol = sendCommandProtocol;
        }

        private void Start()
        {
            submitButton.onClick.AddListener(SubmitCommand);
        }

        private void SubmitCommand()
        {
            _sendCommandProtocol.SendCommand(commandInputField.text);
        }
    }
}