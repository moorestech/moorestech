using System;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Client.Story.Test
{
    public class StoryManagerTest : MonoBehaviour
    {
        [SerializeField] private StoryManager _storyManager;
        [SerializeField] private TextAsset _scenarioCsv;

        private void Start()
        {
            _storyManager.StartStory(_scenarioCsv).Forget();
        }
    }
}