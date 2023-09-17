using System;
using System.Collections.Generic;
using UnityEngine;

namespace MainGame.MooresNovel.ScriptData
{
    [CreateAssetMenu(fileName = "NovelCharacter", menuName = "NovelCharacter", order = 0)]
    public class MooresNovelCharacter : ScriptableObject
    {
        [SerializeField] private List<ScriptCharacterData> characters;


        public ScriptCharacterData GetCharacter(string characterKey)
        {
            foreach (var character in characters)
            {
                if (character.Key == characterKey)
                {
                    return character;
                }
            }
            Debug.LogError("キャラクターのKeyがありません key:" + characterKey);
            return null;
        }
    }
    
    [Serializable]
    public class ScriptCharacterData
    {
        [SerializeField] private string name;
        public string Name => name;
        [SerializeField] private string key;
        public string Key => key;
        [SerializeField] private Sprite characterSprite;
        public Sprite CharacterSprite => characterSprite;
    }
}