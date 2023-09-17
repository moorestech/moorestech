using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using CsvHelper;
using UnityEngine;

namespace MooresNovel.ScriptData
{
    [CreateAssetMenu(fileName = "StoryScript", menuName = "MooresNovel/StoryScript", order = 0)]
    public class MooresNovelScript : ScriptableObject
    {
        [SerializeField] private List<MooresNovelEventScripts> eventScripts;


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

}