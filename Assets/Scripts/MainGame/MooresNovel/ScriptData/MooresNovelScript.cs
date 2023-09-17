using System;
using System.Collections.Generic;
using UnityEngine;

namespace MainGame.MooresNovel.ScriptData
{
    [CreateAssetMenu(fileName = "StoryScript", menuName = "StoryScript", order = 0)]
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
        [SerializeField] private List<MooresNovelLine> scripts;
        public List<MooresNovelLine> Scripts => scripts;
    }

    [Serializable]
    public class MooresNovelLine
    {
        [SerializeField] private string characterKey;
        public string CharacterKey => characterKey;
        [SerializeField] private string text;
        public string Text => text;
    }

}