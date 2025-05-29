using System.Collections.Generic;
using Client.Skit.Define;
using Client.Skit.Skit;
using Client.Skit.UI;
using UnityEngine;


namespace CommandForgeGenerator.Command
{
    public class StoryContext
    {
        private readonly Dictionary<string, SkitCharacter> _characters;
        public readonly ISkitCamera SkitCamera;
        public readonly SkitUI SkitUI;
        public readonly VoiceDefine VoiceDefine;
        
        public StoryContext(SkitUI skitUI, Dictionary<string, SkitCharacter> characters, SkitCamera skitCamera, VoiceDefine voiceDefine)
        {
            SkitUI = skitUI;
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
            foreach (var character in _characters) Object.Destroy(character.Value.gameObject);
        }
    }
}