using Client.Localization;
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

        private void Start()
        {
            tmpDropdown.ClearOptions();
            tmpDropdown.AddOptions(LanguageSelection.GetDisplayNames());
            ShowCurrentLanguage();
            tmpDropdown.onValueChanged.AddListener(OnValueChanged);

            // 他のUI・起動時適用に追従
            // Follow the other dropdown and startup language application
            Localize.OnLanguageChanged.Subscribe(_ => ShowCurrentLanguage()).AddTo(this);
        }

        private void ShowCurrentLanguage()
        {
            tmpDropdown.SetValueWithoutNotify(LanguageSelection.GetCurrentIndex());
        }

        private void OnValueChanged(int index)
        {
            LanguageSelection.TrySetByIndex(index);
        }
    }
}
