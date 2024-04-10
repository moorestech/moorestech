using System;
using UnityEngine;

namespace Client.Story.Test
{
    public class StoryManagerTest : MonoBehaviour
    {
        [SerializeField] private StoryManager _storyManager;

        private void Start()
        {
            _storyManager.StartStory();
        }
    }
}