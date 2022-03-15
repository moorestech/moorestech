using TMPro;
using UnityEngine;

namespace MainGame.UnityView.UI.Inventory.View
{
    public class ItemNameText : MonoBehaviour
    {
        [SerializeField] private TMP_Text nameText;
        public void SetText(string text)
        {
            nameText.text = text;
        }

        public void SetActive(bool active)
        {
            gameObject.SetActive(active);
        }
    }
}