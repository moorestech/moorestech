using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using CsvHelper;
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
        private static readonly Dictionary<string, Dictionary<string, string>> localizeDictionary = new();

        public static IObservable<Unit> OnLanguageChanged => _onLanguageChangedSubject;
        private static Subject<Unit> _onLanguageChangedSubject = new();

        private static string CurrentLanguageCode { get; set; }

        private const string DefaultLanguageCode = "english";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        public static void Initialize()
        {
            //player prefsから言語コードを取得
            CurrentLanguageCode = PlayerPrefs.GetString("LanguageCode", DefaultLanguageCode);

            // CSVファイルのパス
            var csvFilePath = Path.Combine(ServerConst.ServerDirectory, "config", "localization.csv");

            var languageCodes = new List<string>();
            bool isFirstRow = true;

            using var reader = new StreamReader(csvFilePath);
            using var csv = new CsvReader(reader, CultureInfo.InvariantCulture);
            while (csv.Read())
            {
                if (isFirstRow)
                {
                    // csvの1行目は言語コードなので、それを取得
                    for (var i = 0; csv.TryGetField<string>(i, out var field); i++)
                    {
                        languageCodes.Add(field);
                        localizeDictionary.Add(field, new Dictionary<string, string>());
                    }
                    isFirstRow = false;
                    continue;
                }

                var keyAndValues = new List<string>();
                for (var i = 0; csv.TryGetField<string>(i, out var field); i++)
                {
                    keyAndValues.Add(field);
                }

                var key = keyAndValues[0];
                for (var i = 1; i < keyAndValues.Count; i++)
                {
                    //外部ソースから取得したテキストには改行コードが\nとして入っているので、それを\nに変換
                    localizeDictionary[languageCodes[i]].Add(key, keyAndValues[i].Replace("\\n", "\n"));
                }
            }
        }

        public static string Get(string key)
        {
            if (localizeDictionary[CurrentLanguageCode].TryGetValue(key, out var value))
            {
                return value;
            }
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