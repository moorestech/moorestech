using System.Collections.Generic;
using Client.Skit.Define;
using Client.Skit.UI;
using UnityEngine;

namespace Client.Skit.SkitTrack
{
    public class StoryContext
    {
        private readonly Dictionary<string, SkitCharacter> _characters;
        public readonly ISkitCamera SkitCamera;
        public readonly SkitMainUI SkitMainUI;
        public readonly VoiceDefine VoiceDefine;

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
            foreach (KeyValuePair<string, SkitCharacter> character in _characters)
            {
                Object.Destroy(character.Value.gameObject);
            }
        }
    }
}