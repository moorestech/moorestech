using System;
using System.Collections.Generic;
using System.Linq;
using Client.Game.InGame.Context;
using Client.Game.InGame.UI.ContextMenu;
using Client.Game.InGame.UI.Inventory.Common;
using Client.Game.InGame.UI.Inventory.RecipeViewer;
using Core.Master;
using Game.CraftTree.Models;
using TMPro;
using UniRx;
using UnityEngine;
using UnityEngine.UI;

namespace Client.Game.InGame.CraftTree.TreeView
{
    public class CraftTreeEditorNodeItem : MonoBehaviour
    {
        [SerializeField] private TMP_Text itemNameText;
        [SerializeField] private ItemSlotView itemSlotView;
        [SerializeField] private GameObject completedObject;
        
        [SerializeField] private RectTransform offsetUiTransform;
        [SerializeField] private float depthWidth = 50f;
        
        [SerializeField] private UGuiContextMenuTarget contextMenuTarget;
        [SerializeField] private Button expandButton;
        
        public IObservable<Unit> OnUpdateNode => _onUpdateNode;
        private readonly Subject<Unit> _onUpdateNode = new();
        
        public CraftTreeNode Node { get; private set; }
        private ItemRecipeViewerDataContainer _itemRecipeViewerDataContainer;
        
        private void Awake()
        {
            expandButton.onClick.AddListener(ExpandNode);
        }
        
        public void Initialize(CraftTreeNode node, int depth, ItemRecipeViewerDataContainer itemRecipeViewerDataContainer)
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
                itemSlotView.SetItem(itemView, 0);
                
                completedObject.SetActive(node.IsCompleted);
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
                    new("レシピを非表示", HideChildrenNode),
                    new("これより下を完了", () => { ChildrenComplete(Node); }),
                    new("これより上を未完了に", () => { ParentUnComplete(Node); }),
                };
                contextMenuTarget.SetContextMenuBars(contextMenus);
            }
            
            void HideChildrenNode()
            {
                Node.ReplaceChildren(new List<CraftTreeNode>());
                _onUpdateNode.OnNext(Unit.Default);
            }
            
            
            void ChildrenComplete(CraftTreeNode targetNode)
            {
                targetNode.SetCurrentItemCount(targetNode.RequiredCount);
                foreach (var child in targetNode.Children)
                {
                    ChildrenComplete(child);
                }
                _onUpdateNode.OnNext(Unit.Default);
            }
            
            void ParentUnComplete(CraftTreeNode treeNode)
            {
                treeNode.SetCurrentItemCount(0);
                var parent = treeNode.Parent;
                if (parent != null)
                {
                    ParentUnComplete(parent);
                }
                
                // ノードの更新イベントを発行
                _onUpdateNode.OnNext(Unit.Default);
            }
            
            #endregion
        }
        
        private void ExpandNode()
        {
            // すでにレシピが登録されている場合は何もしない
            // If there are already recipes registered, do nothing
            if (Node.Children.Count != 0) return;
            
            var targetItem = Node.TargetItemId;
            var itemRecipes = _itemRecipeViewerDataContainer.GetItem(targetItem);
            if (itemRecipes == null) return;
            
            var children = GetMaterialItems(itemRecipes);
            
            Node.ReplaceChildren(children);
            
            _onUpdateNode.OnNext(Unit.Default);
            
            
            #region Internal
            
            List<CraftTreeNode> GetMaterialItems(RecipeViewerItemRecipes recipes)
            {
                var materials = new List<CraftTreeNode>();
                if (recipes.UnlockedCraftRecipes().Count == 0 && recipes.MachineRecipes.Count != 0)
                {
                    var machineRecipe = recipes.MachineRecipes.FirstOrDefault();
                    foreach (var inputItem in machineRecipe.Value.First().InputItems)
                    {
                        var itemId = MasterHolder.ItemMaster.GetItemId(inputItem.ItemGuid);
                        var count = inputItem.Count * Node.RequiredCount;
                        materials.Add(new CraftTreeNode(itemId, count, Node));
                    }
                    
                    var blockItemId = MasterHolder.BlockMaster.GetItemId(machineRecipe.Key);
                    materials.Add(new CraftTreeNode(blockItemId, 1, Node));
                    
                    return materials;
                }
                
                foreach (var recipe in recipes.UnlockedCraftRecipes())
                {
                    foreach (var recipeItem in recipe.RequiredItems)
                    {
                        var itemId = MasterHolder.ItemMaster.GetItemId(recipeItem.ItemGuid);
                        
                        var count = recipeItem.IsRemain ?? false ?
                            recipeItem.Count :
                            recipeItem.Count * Node.RequiredCount;
                        
                        
                        materials.Add(new CraftTreeNode(itemId, count, Node));
                    }
                }
                
                return materials;
            }
            
            #endregion
        }
    }
}