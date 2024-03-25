using System.Collections.Generic;

namespace Client.Story
{
    public class StoryContext
    {
        public readonly StoryUI StoryUI;
        public readonly IStoryCamera StoryCamera;
        private readonly Dictionary<string, StoryCharacter> _characters;
        
        public StoryContext(StoryUI storyUI, Dictionary<string, StoryCharacter> characters, StoryCamera storyCamera)
        {
            StoryUI = storyUI;
            _characters = characters;
            StoryCamera = storyCamera;
        }
        
        public StoryCharacter GetCharacter(string characterKey)
        {
            return _characters[characterKey];
        }
    }
}