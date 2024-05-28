using Client.Game.InGame.Context;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Client.Game.InGame.Presenter.Command
{
    public class CommandUIInput : MonoBehaviour
    {
        [SerializeField] private TMP_InputField commandInputField;
        [SerializeField] private Button submitButton;


        private void Start()
        {
            submitButton.onClick.AddListener(SubmitCommand);
        }

        private void SubmitCommand()
        {
            ClientContext.VanillaApi.SendOnly.SendCommand(commandInputField.text);
            commandInputField.text = string.Empty;
        }
    }
}