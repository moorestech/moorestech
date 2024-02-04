using MainGame.Network.Send;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace MainGame.Presenter.Command
{
    public class CommandUIInput : MonoBehaviour
    {
        [SerializeField] private TMP_InputField commandInputField;
        [SerializeField] private Button submitButton;

        private SendCommandProtocol _sendCommandProtocol;

        private void Start()
        {
            submitButton.onClick.AddListener(SubmitCommand);
        }


        [Inject]
        public void Construct(SendCommandProtocol sendCommandProtocol)
        {
            _sendCommandProtocol = sendCommandProtocol;
        }

        private void SubmitCommand()
        {
            _sendCommandProtocol.SendCommand(commandInputField.text);
            commandInputField.text = string.Empty;
        }
    }
}