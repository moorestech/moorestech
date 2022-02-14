using System;
using UnityEngine;
using UnityEngine.UI;

namespace MainGame.Control.UI.Command
{
    public class CommandUIInput: MonoBehaviour
    {
        [SerializeField] private InputField commandInputField;
        [SerializeField] private Button submitButton;
        
        

        private void Start()
        {
            submitButton.onClick.AddListener(SubmitCommand);
        }

        private void SubmitCommand()
        {
            
        }
    }
}