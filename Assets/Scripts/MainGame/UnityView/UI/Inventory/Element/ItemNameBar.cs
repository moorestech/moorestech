using System;
using TMPro;
using UnityEngine;

namespace MainGame.UnityView.UI.Inventory.Element
{
    public interface IItemNameBar
    {
        public void HideItemName();
        public void ShowItemName(string name);

    }
    public class ItemNameBar : MonoBehaviour,IItemNameBar
    {
        [SerializeField] private GameObject itemNameBar;
        [SerializeField] private TMP_Text itemName;
        
        public static IItemNameBar Instance { get; private set; }

        private void Awake()
        {
            Instance = this;
        }
        
        public void ShowItemName(string name)
        {
            itemNameBar.SetActive(true);
            itemName.text = name;
        }

        
        public void HideItemName()
        {
            itemNameBar.SetActive(false);
        }
    }
}