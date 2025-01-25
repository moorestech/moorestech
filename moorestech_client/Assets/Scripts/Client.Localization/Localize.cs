using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Client.Common;
using CsvHelper;
using UniRx;
using UnityEngine;

namespace Client.Localization
{
    public static class Localize
    {
        private const string DefaultLanguageCode = "english";
        private const int StartLocalizeTextIndex = 2;
        
        /// <summary>
        ///     ローカライズ用のテキストが入っている
        ///     Key : 国コード
        ///     Value : キーとテキストのペア
        /// </summary>
        private static readonly Dictionary<string, Dictionary<string, string>> localizeDictionary = new();
        
        private static readonly Subject<Unit> _onLanguageChangedSubject = new();
        
        public static IObservable<Unit> OnLanguageChanged => _onLanguageChangedSubject;
        
        public static string CurrentLanguageCode { get; private set; }
        public static List<string> LanguageCodes => localizeDictionary.Keys.ToList();
        
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        public static void Initialize()
        {
            //player prefsから言語コードを取得
            CurrentLanguageCode = PlayerPrefs.GetString("LanguageCode", DefaultLanguageCode);
            
            // CSVファイルのパス
            var csvFilePath = Path.Combine(ServerConst.DefaultServerDirectory, "config", "localization.csv");
            
            var languageCodes = new List<string>();
            var isFirstRow = true;
            
            using var reader = new StreamReader(csvFilePath);
            using var csv = new CsvReader(reader, CultureInfo.InvariantCulture);
            while (csv.Read())
            {
                if (isFirstRow)
                {
                    // csvの1行目は言語コードなので、それを取得
                    // 1列目はキー、2列目はソース文字、3列目以降は言語コードなので2から回す
                    for (var i = StartLocalizeTextIndex; csv.TryGetField<string>(i, out var field); i++)
                    {
                        languageCodes.Add(field);
                        localizeDictionary.Add(field, new Dictionary<string, string>());
                    }
                    
                    isFirstRow = false;
                    continue;
                }
                
                var keyAndValues = new List<string>();
                for (var i = 0; csv.TryGetField<string>(i, out var field); i++) keyAndValues.Add(field);
                
                var key = keyAndValues[0];
                for (var i = StartLocalizeTextIndex; i < keyAndValues.Count; i++)
                    //外部ソースから取得したテキストには改行コードが\nとして入っているので、それを\nに変換
                    localizeDictionary[languageCodes[i - 2]].Add(key, keyAndValues[i].Replace("\\n", "\n"));
            }
        }
        
        public static string Get(string key)
        {
            if (localizeDictionary[CurrentLanguageCode].TryGetValue(key, out var value)) return value;
            return $"[Localize] Key : {key} is not found";
        }
        
        public static void SetLanguage(string languageCode)
        {
            if (localizeDictionary.ContainsKey(languageCode))
            {
                CurrentLanguageCode = languageCode;
                PlayerPrefs.SetString("LanguageCode", languageCode);
                PlayerPrefs.Save();
                _onLanguageChangedSubject?.OnNext(Unit.Default);
            }
            else
            {
                Debug.LogError($"[Localize] Language Code : {languageCode} is not found");
            }
        }
    }
}