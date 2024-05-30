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
            tmpDropdown.AddOptions(Localize.LanguageCodes);
            tmpDropdown.value = Localize.LanguageCodes.IndexOf(Localize.CurrentLanguageCode);
            tmpDropdown.onValueChanged.AddListener(OnValueChanged);
        }
        
        private void OnValueChanged(int index)
        {
            Localize.SetLanguage(Localize.LanguageCodes[index]);
        }
    }
}