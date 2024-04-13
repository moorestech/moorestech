using System.Collections.Generic;
using Client.Story.UI;
using UnityEngine;

namespace Client.Story
{
    public class StoryContext
    {
        public readonly StoryUI StoryUI;
        public readonly IStoryCamera StoryCamera;
        public readonly VoiceDefine VoiceDefine;
        private readonly Dictionary<string, StoryCharacter> _characters;

        public StoryContext(StoryUI storyUI, Dictionary<string, StoryCharacter> characters, StoryCamera storyCamera, VoiceDefine voiceDefine)
        {
            StoryUI = storyUI;
            _characters = characters;
            StoryCamera = storyCamera;
            VoiceDefine = voiceDefine;
        }

        public StoryCharacter GetCharacter(string characterKey)
        {
            return _characters[characterKey];
        }

        public void DestroyCharacter()
        {
            foreach (var character in _characters)
            {
                Object.Destroy(character.Value.gameObject);
            }
        }
    }
}