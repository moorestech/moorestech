using System;
using Client.Game.Story.UI;
using UnityEngine;

namespace Client.Game.Story
{
    public class PlayerStoryStarter : MonoBehaviour
    {
        [SerializeField] private StartStoryUI startStoryUI;
        
        public bool IsStartReady => CurrentStoryStarterObject != null;
        public StoryStarterObject CurrentStoryStarterObject { get; private set; }


        private void OnTriggerEnter(Collider other)
        {
            if (other.TryGetComponent<StoryStarterObject>(out var storyStarterObject))
            {
                CurrentStoryStarterObject = storyStarterObject;
                startStoryUI.ShowStartStoryUI(true);
            }
        }
        
        private void OnTriggerExit(Collider other)
        {
            if (other.TryGetComponent<StoryStarterObject>(out var _))
            {
                CurrentStoryStarterObject = null;
                startStoryUI.ShowStartStoryUI(false);
            }
        }
    }
}