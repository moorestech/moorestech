using System;
using Client.Game.InGame.UI.Inventory.Element;
using Client.Game.InGame.UI.Inventory.Sub;
using Core.Master;
using TMPro;
using UniRx;
using UnityEngine;
using UnityEngine.UI;

namespace Client.Game.InGame.UI.Inventory.RecipeViewer
{
    public class MachineRecipeView : MonoBehaviour
    {
        [SerializeField] private ItemSlotObject itemSlotObjectPrefab;
        
        [SerializeField] private RectTransform materialParent;
        [SerializeField] private RectTransform resultParent;
        
        [SerializeField] private Button nextRecipeButton;
        [SerializeField] private Button prevRecipeButton;
        
        [SerializeField] private TMP_Text itemNameText;
        [SerializeField] private TMP_Text recipeCountText;
        
        public IObservable<RecipeViewerItemRecipes> OnClickItem => _onClickItem;
        private readonly Subject<RecipeViewerItemRecipes> _onClickItem = new();
        
        public void SetRecipes(RecipeViewerItemRecipes recipeViewerItemRecipes)
        {
        }
        
        public void SetBlockId(BlockId blockId)
        {
        }
        
        public void SetActive(bool enable)
        {
            gameObject.SetActive(enable);
        }
    }
}