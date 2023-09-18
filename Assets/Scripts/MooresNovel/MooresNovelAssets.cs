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
        [SerializeField] private List<MooresNovelScenario> eventScripts;
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


        public MooresNovelScenario GetScenario(string key)
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
    public class MooresNovelScenario
    {
        [SerializeField] private string key;
        public string Key => key;
        [SerializeField] private TextAsset scenarioCsv;

        public List<MooresNovelLine> CreateScenario()
        {
            using var csv = new CsvReader(new StringReader(scenarioCsv.text), CultureInfo.InvariantCulture);
            var result = new List<MooresNovelLine>();
            while (csv.Read())
            {
                var characterKey = csv.GetField<string>(0);
                var backgroundKey = csv.GetField<string>(1);
                var text = csv.GetField<string>(2);
                result.Add(new MooresNovelLine(characterKey, text,backgroundKey));
            }

            return result;
        }
    }
    
    public class MooresNovelLine
    {
        public readonly string CharacterKey;
        public readonly string BackgroundKey;
        public readonly string Text;

        public MooresNovelLine(string characterKey, string text, string backgroundKey)
        {
            CharacterKey = characterKey;
            Text = text;
            BackgroundKey = backgroundKey;
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