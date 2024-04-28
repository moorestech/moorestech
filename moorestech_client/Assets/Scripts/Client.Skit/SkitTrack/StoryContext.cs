using System.Collections.Generic;
using Client.Story.UI;
using UnityEngine;

namespace Client.Story
{
    public class StoryContext
    {
        public readonly SkitMainUI SkitMainUI;
        public readonly ISkitCamera SkitCamera;
        public readonly VoiceDefine VoiceDefine;
        private readonly Dictionary<string, SkitCharacter> _characters;

        public StoryContext(SkitMainUI skitMainUI, Dictionary<string, SkitCharacter> characters, SkitCamera skitCamera, VoiceDefine voiceDefine)
        {
            SkitMainUI = skitMainUI;
            _characters = characters;
            SkitCamera = skitCamera;
            VoiceDefine = voiceDefine;
        }

        public SkitCharacter GetCharacter(string characterKey)
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