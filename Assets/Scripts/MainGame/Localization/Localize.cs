using System;
using System.Collections.Generic;
using System.IO;
using GameConst;
using UniRx;
using UnityEngine;

namespace MainGame.Localization
{
    public static class Localize
    {
        /// <summary>
        /// ローカライズ用のテキストが入っている
        /// Key : 国コード
        /// Value : キーとテキストのペア
        /// </summary>
        private static readonly Dictionary<string,Dictionary<string,string>> localizeDictionary = new();
        
        public static IObservable<Unit> OnLanguageChanged => _onLanguageChangedSubject;
        private static Subject<Unit> _onLanguageChangedSubject = new();

        private static string CurrentLanguageCode { get; set; }
        
        private const string DefaultLanguageCode = "english";


        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)] 
        public static void Initialize()
        {
            //player prefsから言語コードを取得
            CurrentLanguageCode = PlayerPrefs.GetString("LanguageCode", DefaultLanguageCode);
            
            //languageファイルをcsvから読み込む
            var csvText = File.ReadAllText(Path.Combine(ServerConst.ServerDirectory, "config", "localization.csv"));
            var csvLines = csvText.Split('\n');
            
            
            //csvの1行目は言語コードなので、それを取得
            var languageCodes = csvLines[0].Split(',');
            foreach (var langCode in languageCodes)
            {
                localizeDictionary.Add(langCode,new Dictionary<string, string>());
            }

            //実際に言語ファイルを読み込む
            for (int i = 1; i < csvLines.Length; i++)
            {
                var keyAndValues = csvLines[i].Split(',');
                var key = keyAndValues[0];
                for (int j = 1; j < keyAndValues.Length; j++)
                {
                    var value = keyAndValues[j];
                    localizeDictionary[languageCodes[j]].Add(key,value);
                }
            }

        }
        
        
        public static string Get(string key)
        {
            if (localizeDictionary[CurrentLanguageCode].TryGetValue(key,out var value))
            {
                return value;
            }
            return $"[Localize Error] Key : {key} is not found";
        }
        
        
        public static void SetLanguage(string languageCode)
        {
            if (localizeDictionary.ContainsKey(languageCode))
            {
                CurrentLanguageCode = languageCode;
                PlayerPrefs.SetString("LanguageCode",languageCode);
                PlayerPrefs.Save();
                _onLanguageChangedSubject?.OnNext(Unit.Default);
            }
            else
            {
                Debug.LogError($"[Localize Error] Language Code : {languageCode} is not found");
            }
        }
    }
}