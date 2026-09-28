using System.Collections.Generic;
using Client.Localization;
using Mooresmaster.Localization.Generated;
using TMPro;
using UniRx;
using UnityEngine;

namespace Client.MainMenu
{
    // 母国語表記で並べ、別経路の言語変更にも追従する
    // List native names and follow language changes made elsewhere
    public class LanguageSetting : MonoBehaviour
    {
        [SerializeField] private TMP_Dropdown tmpDropdown;

        private readonly List<string> languageCodes = new();

        private void Start()
        {
            // CSVの順序で表示名とコードを対応させる
            // Keep names and codes aligned in CSV order
            var displayNames = new List<string>();
            foreach (var language in LanguageCatalog.Languages)
            {
                languageCodes.Add(language.Code);
                displayNames.Add(language.DisplayName);
            }

            tmpDropdown.ClearOptions();
            tmpDropdown.AddOptions(displayNames);
            ShowCurrentLanguage();
            tmpDropdown.onValueChanged.AddListener(OnValueChanged);

            // 他のドロップダウンや起動時適用にも表示を合わせる
            // Follow the other dropdown and startup language application
            Localize.OnLanguageChanged.Subscribe(_ => ShowCurrentLanguage()).AddTo(this);
        }

        private void ShowCurrentLanguage()
        {
            tmpDropdown.SetValueWithoutNotify(languageCodes.IndexOf(Localize.GetCurrentLanguageCode()));
        }

        private void OnValueChanged(int index)
        {
            // 選択肢は有効な言語だけを含む
            // Options contain only selectable languages
            Localize.TrySetLanguage(languageCodes[index]);
        }
    }
}
