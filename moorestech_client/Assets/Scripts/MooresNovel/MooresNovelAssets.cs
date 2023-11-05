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
        [SerializeField] private List<NovelCharacterSpriteData> characters;
        [SerializeField] private List<NovelBackgroundSpriteData> backgrounds;


        public NovelCharacterSpriteData GetCharacter(string characterKey)
        {
            foreach (var character in characters)
                if (character.Key == characterKey)
                    return character;
            Debug.LogError("キャラクターのKeyがありません key:" + characterKey);
            return null;
        }

        public NovelBackgroundSpriteData GetBackground(string backgroundKey)
        {
            foreach (var background in backgrounds)
                if (background.Key == backgroundKey)
                    return background;
            Debug.LogError("背景のKeyがありません key:" + backgroundKey);
            return null;
        }


        public MooresNovelScenario GetScenario(string key)
        {
            foreach (var eventScript in eventScripts)
                if (eventScript.Key == key)
                    return eventScript;
            Debug.LogError("スクリプトのKeyがありません key:" + key);
            return null;
        }
    }

    [Serializable]
    public class MooresNovelScenario
    {
        [SerializeField] private string key;
        [SerializeField] private TextAsset scenarioCsv;
        public string Key => key;

        public List<IMooresNovelEvent> CreateScenario()
        {
            using var csv = new CsvReader(new StringReader(scenarioCsv.text), CultureInfo.InvariantCulture);
            var result = new List<IMooresNovelEvent>();
            while (csv.Read())
            {
                var type = csv.GetField<string>(0);
                if (type == MooresNovelEventType.Line.ToString())
                {
                    var characterKey = csv.GetField<string>(1);
                    var backgroundKey = csv.GetField<string>(2);
                    var text = csv.GetField<string>(3);
                    result.Add(new MooresNovelLine(characterKey, text, backgroundKey));
                }
                else if (type == MooresNovelEventType.Transition.ToString())
                {
                    result.Add(new MoresNovelTranslation());
                }
                else
                {
                    Debug.LogError("csvの1列目が不正です type:" + type);
                }
            }

            return result;
        }
    }

    public interface IMooresNovelEvent
    {
        public MooresNovelEventType EventType { get; }
    }

    public class MooresNovelLine : IMooresNovelEvent
    {
        public readonly string BackgroundKey;
        public readonly string CharacterKey;
        public readonly string Text;

        public MooresNovelLine(string characterKey, string text, string backgroundKey)
        {
            CharacterKey = characterKey;
            Text = text;
            BackgroundKey = backgroundKey;
        }

        public MooresNovelEventType EventType => MooresNovelEventType.Line;
    }

    public class MoresNovelTranslation : IMooresNovelEvent
    {
        public MooresNovelEventType EventType => MooresNovelEventType.Transition;
    }

    public enum MooresNovelEventType
    {
        Line,
        Transition
    }


    [Serializable]
    public class NovelCharacterSpriteData
    {
        [SerializeField] private string name;
        [SerializeField] private string key;
        [SerializeField] private Sprite characterSprite;
        public string Name => name;
        public string Key => key;
        public Sprite CharacterSprite => characterSprite;
    }

    [Serializable]
    public class NovelBackgroundSpriteData
    {
        [SerializeField] private string key;
        [SerializeField] private Sprite characterSprite;
        public string Key => key;
        public Sprite CharacterSprite => characterSprite;
    }
}