using System.Collections.Generic;

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
    }
}