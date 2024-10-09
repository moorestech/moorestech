using System;
using Client.Game.InGame.UI.Inventory.Sub;
using Core.Master;
using UniRx;
using UnityEngine;

namespace Client.Game.InGame.UI.Inventory.RecipeViewer
{
    public class MachineRecipeView : MonoBehaviour
    {
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