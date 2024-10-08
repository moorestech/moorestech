using Client.Game.InGame.UI.Inventory.Sub;
using UnityEngine;

namespace Client.Game.InGame.UI.Inventory.RecipeViewer
{
    public class RecipeViewerView : MonoBehaviour
    {
        [SerializeField] private CraftInventoryView craftInventoryView;
        [SerializeField] private MachineRecipeView machineRecipeView;
        [SerializeField] private RecipeTabView recipeTabView;
    }
}