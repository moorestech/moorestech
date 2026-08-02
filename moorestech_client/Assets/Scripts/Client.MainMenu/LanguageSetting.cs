using Client.Localization;
using TMPro;
using UnityEngine;

namespace Client.MainMenu
{
    public class LanguageSetting : MonoBehaviour
    {
        [SerializeField] private TMP_Dropdown tmpDropdown;
        
        private void Start()
        {
            tmpDropdown.ClearOptions();
            var languageCodes = Localize.GetLanguageCodes();
            tmpDropdown.AddOptions(languageCodes);
            tmpDropdown.value = languageCodes.IndexOf(Localize.GetCurrentLanguageCode());
            tmpDropdown.onValueChanged.AddListener(OnValueChanged);
        }
        
        private void OnValueChanged(int index)
        {
            // 選択肢は選択可能な言語だけなので可否の戻り値は捨ててよい
            // Options contain only selectable languages, so the result can be discarded
            Localize.TrySetLanguage(Localize.GetLanguageCodes()[index]);
        }
    }
}
