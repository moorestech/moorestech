using System;
using MainGame.Basic;
using MainGame.UnityView.UI.Inventory.Element;
using TMPro;
using UnityEngine;
using VContainer;

namespace MainGame.UnityView.UI.CraftRecipe
{
    public class RecipeViewerItemNamePresenter : MonoBehaviour
    {
        [SerializeField] private TMP_Text itemNameText;
        [SerializeField] private GameObject itemNameTextGameObject;

        [SerializeField] private ItemRecipeView itemRecipeView;
        [SerializeField] private CraftRecipeItemListViewer craftRecipeItemListViewer;

        private ItemImages _itemImages;

        [Inject]
        private void Construct(ItemImages itemImages)
        {
            _itemImages = itemImages;
        }
        
        
        
        private void Start()
        {
            itemRecipeView.OnCursorEnter += OnCursorEnter;
            craftRecipeItemListViewer.OnCursorEnter += OnItemListCursorEnter;
            
            itemRecipeView.OnCursorExit += _ => itemNameTextGameObject.gameObject.SetActive(false);
            craftRecipeItemListViewer.OnCursorExit += _ => itemNameTextGameObject.gameObject.SetActive(false);
        }

        private void OnItemListCursorEnter(int id)
        {
            if (id == ItemConstant.NullItemId)
            {
                return;
            }
            itemNameTextGameObject.gameObject.SetActive(true);
            itemNameText.text = _itemImages.GetItemView(id).itemName;
        }

        private void OnCursorEnter(ItemStack item)
        {
            if (item.ID == ItemConstant.NullItemId)
            {
                return;
            }
            itemNameTextGameObject.gameObject.SetActive(true);
            itemNameText.text = _itemImages.GetItemView(item.ID).itemName;
        }
    }
}