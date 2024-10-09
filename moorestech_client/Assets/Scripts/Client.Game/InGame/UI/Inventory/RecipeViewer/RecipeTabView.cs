using System;
using System.Collections.Generic;
using Client.Game.InGame.Context;
using Client.Game.InGame.UI.Inventory.Sub;
using Core.Master;
using UniRx;
using UnityEngine;

namespace Client.Game.InGame.UI.Inventory.RecipeViewer
{
    public class RecipeTabView : MonoBehaviour
    {
        [SerializeField] private RecipeViewerTabElement tabElementPrefab;
        [SerializeField] private Transform tabElementParent;
        
        public IObservable<BlockId?> OnClickTab => onClickTab; // nullならCraftを選択したことを意味する
        private readonly Subject<BlockId?> onClickTab = new(); // If null, it means that Craft is selected
        
        private List<RecipeViewerTabElement> _currentTabs;
        
        public void SetRecipeTabView(RecipeViewerItemRecipes recipes)
        {
            foreach (var tab in _currentTabs)
            {
                Destroy(tab.gameObject);
            }
            
            _currentTabs.Clear();
            
            if (recipes.CraftRecipes.Count != 0)
            {
                var tabElement = Instantiate(tabElementPrefab, tabElementParent);
                tabElement.Initialize();
                tabElement.SetCraftIcon();
                tabElement.SetSelected(true);
                _currentTabs.Add(tabElement);
            }
            
            foreach (var machineRecipe in recipes.MachineRecipes)
            {
                var blockId = machineRecipe.Key;
                var itemId = MasterHolder.BlockMaster.GetItemId(blockId);
                var blockItemView = ClientContext.ItemImageContainer.GetItemView(itemId);
                
                var tabElement = Instantiate(tabElementPrefab, tabElementParent);
                tabElement.Initialize();
                tabElement.SetMachineItem(blockItemView);
                _currentTabs.Add(tabElement);
            }
        }
    }
}