using Client.Game.InGame.UI.Inventory.RecipeViewer;
using Core.Master;
using Game.CraftTree;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace Client.Game.InGame.CraftTree.TreeView
{
    public class CraftTreeViewManager : MonoBehaviour
    {
        [SerializeField] private Button hideButton;
        [SerializeField] private CraftTreeEditorView craftTreeEditorView;
        
        private void Awake()
        {
            hideButton.onClick.AddListener(Hide);
        }
        
        [Inject]
        public void Construct(ItemRecipeViewerDataContainer itemRecipe)
        {
            craftTreeEditorView.Initialize(itemRecipe);
        }
        
        public void Show(ItemId resultItemId)
        {
            var rootNode = new CraftTreeNode(resultItemId, 1);
            craftTreeEditorView.Show(rootNode);
            gameObject.SetActive(true);
        }
        
        public void Hide()
        {
            gameObject.SetActive(false);
        }
    }
}