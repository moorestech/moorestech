using System;
using Client.Game.Story.UI;
using UnityEngine;

namespace Client.Game.Story
{
    public class PlayerSkitStarterDetector : MonoBehaviour
    {
        [SerializeField] private StartSkitUI startSkitUI;

        public bool IsStartReady => CurrentSkitStarterObject != null;
        public SkitStarterObject CurrentSkitStarterObject { get; private set; }


        private void OnTriggerEnter(Collider other)
        {
            if (other.TryGetComponent<SkitStarterObject>(out var storyStarterObject))
            {
                CurrentSkitStarterObject = storyStarterObject;
                startSkitUI.ShowStartStoryUI(true);
            }
        }

        private void OnTriggerExit(Collider other)
        {
            if (other.TryGetComponent<SkitStarterObject>(out var _))
            {
                CurrentSkitStarterObject = null;
                startSkitUI.ShowStartStoryUI(false);
            }
        }

        private void OnDisable()
        {
            startSkitUI.ShowStartStoryUI(false);
        }
    }
}