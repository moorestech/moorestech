using System;
using Client.Game.Sequence;
using UnityEngine;
using UnityEngine.UI;

namespace Client.MainMenu
{
    public class ResetStoryButton : MonoBehaviour
    {
        [SerializeField] private Button resetStoryButton;

        private void Start()
        {
            resetStoryButton.onClick.AddListener(() =>
            {
                PlayerPrefs.DeleteKey(S1InitialMovie.S1InitialMoviePlayerPrefsKey);
                PlayerPrefs.Save();
            });
        }
    }
}