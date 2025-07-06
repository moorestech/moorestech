using System.Collections.Generic;
using Client.Skit.Skit;
using UnityEngine;

namespace Client.Skit.Context
{
    public class CharacterObjectContainer
    {
        private readonly Dictionary<string, SkitCharacter> _characters;
        
        public CharacterObjectContainer(Dictionary<string, SkitCharacter> characters)
        {
            _characters = characters;
        }
        
        public SkitCharacter GetCharacter(string characterKey)
        {
            if (!_characters.TryGetValue(characterKey, out var character))
            {
                Debug.LogError($"キャラクターが見つかりません: {characterKey}");
                return null;
            }
            
            return character;
        }
        
        public void DestroyAllCharacters()
        {
            foreach (var character in _characters) Object.Destroy(character.Value.gameObject);
            _characters.Clear();
        }
    }
}