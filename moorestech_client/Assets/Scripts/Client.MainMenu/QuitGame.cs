﻿using UnityEngine;
using UnityEngine.UI;

namespace Client.MainMenu
{
    public class QuitGame : MonoBehaviour
    {
        [SerializeField] private Button quitButton;

        private void Awake()
        {
            quitButton.onClick.AddListener(Application.Quit);
        }
    }
}