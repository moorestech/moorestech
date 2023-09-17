using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using CsvHelper;
using UnityEngine;

namespace MooresNovel
{
    [CreateAssetMenu(fileName = "MooresNovelAssets", menuName = "MooresNovel/MooresNovelAssets", order = 0)]
    public class MooresNovelAssets : ScriptableObject
    {
        [SerializeField] private List<MooresNovelEventScripts> eventScripts;
        [SerializeField] private List<NovelSpriteData> characters;
        [SerializeField] private List<NovelSpriteData> backgrounds;


        public NovelSpriteData GetCharacter(string characterKey)
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
        
        public NovelSpriteData GetBackground(string backgroundKey)
        {
            foreach (var background in backgrounds)
            {
                if (background.Key == backgroundKey)
                {
                    return background;
                }
            }
            Debug.LogError("背景のKeyがありません key:" + backgroundKey);
            return null;
        }


        public MooresNovelEventScripts GetScript(string key)
        {
            foreach (var eventScript in eventScripts)
            {
                if (eventScript.Key == key)
                {
                    return eventScript;
                }
            }
            Debug.LogError("スクリプトのKeyがありません key:" + key);
            return null;
        }
    }

    [Serializable]
    public class MooresNovelEventScripts
    {
        [SerializeField] private string key;
        public string Key => key;
        [SerializeField] private TextAsset scriptCsv;

        public List<MooresNovelLine> CreateScripts()
        {
            using var csv = new CsvReader(new StringReader(scriptCsv.text), CultureInfo.InvariantCulture);
            var result = new List<MooresNovelLine>();
            while (csv.Read())
            {
                var characterKey = csv.GetField<string>(0);
                var text = csv.GetField<string>(1);
                result.Add(new MooresNovelLine(characterKey, text));
            }

            return result;
        }
    }
    
    public class MooresNovelLine
    {
        public readonly string CharacterKey;
        public readonly string Text;

        public MooresNovelLine(string characterKey, string text)
        {
            CharacterKey = characterKey;
            Text = text;
        }
    }
    
        
    [Serializable]
    public class NovelSpriteData
    {
        [SerializeField] private string name;
        public string Name => name;
        [SerializeField] private string key;
        public string Key => key;
        [SerializeField] private Sprite characterSprite;
        public Sprite CharacterSprite => characterSprite;
    }

}