using System;
using TMPro;
using UnityEngine;

namespace MainGame.UnityView.UI.Inventory.Element
{
    public class ItemNameBar : MonoBehaviour
    {
        [SerializeField] private GameObject itemNameBar;
        [SerializeField] private TMP_Text itemName;
        
        public static ItemNameBar Instance { get; private set; }

        private void Start()
        {
            Instance = this;
        }
        
        public void ShowItemName(string name)
        {
            itemNameBar.SetActive(true);
            itemName.text = name;
        }

        public void ShowItemName()
        {
            itemNameBar.SetActive(true);
        }
        
        public void HideItemName(bool clearText = true)
        {
            if (clearText)
            {
                itemName.text = "";
            }
            itemNameBar.SetActive(false);
        }
    }
}