using Client.Game.Context;
using Client.Network.API;
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


        private void Start()
        {
            submitButton.onClick.AddListener(SubmitCommand);
        }
        
        private void SubmitCommand()
        {
            MoorestechContext.VanillaApi.SendOnly.SendCommand(commandInputField.text);
            commandInputField.text = string.Empty;
        }
    }
}