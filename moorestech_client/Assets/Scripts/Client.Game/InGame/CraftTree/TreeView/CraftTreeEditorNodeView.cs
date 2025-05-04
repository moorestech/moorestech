using System;
using System.Collections.Generic;
using System.Linq;
using Client.Game.InGame.Context;
using Client.Game.InGame.UI.ContextMenu;
using Client.Game.InGame.UI.Inventory.Common;
using Client.Game.InGame.UI.Inventory.RecipeViewer;
using Core.Item.Interface;
using Core.Master;
using Game.Context;
using Game.CraftTree;
using TMPro;
using UniRx;
using UnityEngine;

namespace Client.Game.InGame.CraftTree.TreeView
{
    public class CraftTreeEditorNodeView : MonoBehaviour
    {
        [SerializeField] private TMP_Text itemNameText;
        [SerializeField] private ItemSlotObject itemSlotObject;
        
        [SerializeField] private RectTransform offsetUiTransform;
        [SerializeField] private float depthWidth = 50f;
        
        [SerializeField] private UGuiContextMenuTarget contextMenuTarget;
        
        public IObservable<Unit> OnUpdateNode => _onUpdateNode;
        private readonly Subject<Unit> _onUpdateNode = new();
        
        public CraftTreeNode Node { get; private set; }
        private ItemRecipeViewerDataContainer _itemRecipeViewerDataContainer;
        
        public void Initialize(List<CraftTreeEditorNodeView> children, CraftTreeNode node, int depth, ItemRecipeViewerDataContainer itemRecipeViewerDataContainer)
        {
            Node = node;
            _itemRecipeViewerDataContainer = itemRecipeViewerDataContainer;
            
            SetItem();
            SetPosition();
            SetContextMenu();
            
            #region Internal
            
            void SetItem()
            {
                var itemView = ClientContext.ItemImageContainer.GetItemView(node.TargetItemId);
                itemNameText.text = $"{itemView.ItemName}  {node.CurrentCount} / {node.RequiredCount}";
                itemSlotObject.SetItem(itemView, 0);
            }
            
            void SetPosition()
            {
                var position = offsetUiTransform.anchoredPosition;
                position.x = depth * depthWidth;
                offsetUiTransform.anchoredPosition = position;
            }
            
            void SetContextMenu()
            {
                var contextMenus = new List<ContextMenuBarInfo>
                {
                    new("レシピを展開", ExpandNode),
                    new("レシピを非表示", HideChildrenNode),
                };
                contextMenuTarget.SetContextMenuBars(contextMenus);
            }
            
            #endregion
        }
        
        private void ExpandNode()
        {
            var targetItem = Node.TargetItemId;
            var itemRecipes = _itemRecipeViewerDataContainer.GetItem(targetItem);
            if (itemRecipes == null) return;
            
            var materialItems = GetMaterialItems(itemRecipes);
            
            var children = new List<CraftTreeNode>();
            foreach (var material in materialItems)
            {
                var treeNode = new CraftTreeNode(material.Id, material.Count);
                children.Add(treeNode);
            }
            
            Node.ReplaceChildren(children);
            
            _onUpdateNode.OnNext(Unit.Default);
            
            
            #region Internal
            
            List<IItemStack> GetMaterialItems(RecipeViewerItemRecipes recipes)
            {
                var materialItems = new List<IItemStack>();
                if (recipes.UnlockedCraftRecipes().Count == 0 && recipes.MachineRecipes.Count != 0)
                {
                    var machineRecipe = recipes.MachineRecipes.FirstOrDefault();
                    foreach (var inputItem in machineRecipe.Value.First().InputItems)
                    {
                        var itemStack = ServerContext.ItemStackFactory.Create(inputItem.ItemGuid, inputItem.Count);
                        materialItems.Add(itemStack);
                    }
                    
                    var blockItemId = MasterHolder.BlockMaster.GetItemId(machineRecipe.Key);
                    var blockItemStack = ServerContext.ItemStackFactory.Create(blockItemId, 1);
                    materialItems.Add(blockItemStack);
                    
                    return materialItems;
                }
                
                foreach (var recipe in recipes.UnlockedCraftRecipes())
                {
                    foreach (var item in recipe.RequiredItems)
                    {
                        var itemStack = ServerContext.ItemStackFactory.Create(item.ItemGuid, item.Count);
                        materialItems.Add(itemStack);
                    }
                }
                
                return materialItems;
            }
            
            #endregion
        }
        
        private void HideChildrenNode()
        {
            Node.ReplaceChildren(new List<CraftTreeNode>());
            _onUpdateNode.OnNext(Unit.Default);
        }
    }
}