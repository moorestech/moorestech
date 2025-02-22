using System;
using System.Collections.Generic;
using Client.Game.InGame.Context;
using Client.Game.InGame.UnlockState;
using Core.Master;
using Game.CraftChainer.Util;
using UniRx;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace Client.Game.InGame.UI.Inventory.RecipeViewer
{
    public class RecipeTabView : MonoBehaviour
    {
        [SerializeField] private RecipeViewerTabElement tabElementPrefab;
        [SerializeField] private Transform tabElementParent;
        [SerializeField] private HorizontalLayoutGroup tabElementLayoutGroup; 
        
        public IObservable<BlockId?> OnClickTab => onClickTab; // nullならCraftを選択したことを意味する
        private readonly Subject<BlockId?> onClickTab = new(); // If null, it means that Craft is selected
        
        private readonly List<RecipeViewerTabElement> _currentTabs = new();
        
        private ClientGameUnlockStateDatastore _unlockStateDatastore;
        
        [Inject]
        public void Construct(ClientGameUnlockStateDatastore unlockStateDatastore)
        {
            _unlockStateDatastore = unlockStateDatastore;
        }
        
        public void SetRecipeTabView(RecipeViewerItemRecipes recipes)
        {
            foreach (var tab in _currentTabs)
            {
                Destroy(tab.gameObject);
            }
            
            _currentTabs.Clear();
            
            // クラフトタブがあればそれを優先的異選択
            // If there is a craft tab, select it preferentially
            var isFirstCraft = false;
            var unlockedRecipe = recipes.UnlockedCraftRecipes(_unlockStateDatastore);
            if (unlockedRecipe.Count != 0)
            {
                var tabElement = Instantiate(tabElementPrefab, tabElementParent);
                tabElement.Initialize();
                tabElement.SetCraftIcon();
                tabElement.SetSelected(true);
                tabElement.OnClickTab.Subscribe(OnClickTabAction);
                _currentTabs.Add(tabElement);
                isFirstCraft = true;
            }
            
            var isFirst = true;
            foreach (var machineRecipe in recipes.MachineRecipes)
            {
                var blockId = machineRecipe.Key;
                var itemId = MasterHolder.BlockMaster.GetItemId(blockId);
                var blockItemView = ClientContext.ItemImageContainer.GetItemView(itemId);
                
                var tabElement = Instantiate(tabElementPrefab, tabElementParent);
                tabElement.Initialize();
                tabElement.SetMachineItem(blockId, blockItemView);
                tabElement.OnClickTab.Subscribe(OnClickTabAction);
                _currentTabs.Add(tabElement);
                
                // クラフトタブがない場合は最初のタブを選択
                // If there is no craft tab, select the first tab
                tabElement.SetSelected(isFirst && !isFirstCraft);
                isFirst = false;
            }
            
            // レイアウトの適用を強制  
            // Force the layout to be applied
            UpdateLayoutGroup();
        }
        
        
        private void OnClickTabAction(RecipeViewerTabElement tabElement)
        {
            tabElement.SetSelected(true);
            foreach (var tab in _currentTabs)
            {
                if (tab == tabElement) continue;
                tab.SetSelected(false);
            }
            
            onClickTab.OnNext(tabElement.CurrentBlockId);
            
            UpdateLayoutGroup();
        }
        
        private void UpdateLayoutGroup()
        {
            tabElementLayoutGroup.CalculateLayoutInputHorizontal();
            tabElementLayoutGroup.CalculateLayoutInputVertical();
            
            tabElementLayoutGroup.SetLayoutHorizontal();
            tabElementLayoutGroup.SetLayoutVertical();
        }
    }
}