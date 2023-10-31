using System;
using UnityEngine;
using UnityEngine.UI;

namespace MainGame.UnityView.UI.CraftRecipe
{
    public class RecipePlaceButton : MonoBehaviour
    {
        [SerializeField] private Button placeButton;
        [SerializeField] private ItemRecipePresenter itemRecipePresenter;
        
        public event Action<ViewerRecipeData> OnClick;

        private void Awake()
        {
            placeButton.onClick.AddListener(() => OnClick?.Invoke(itemRecipePresenter.CurrentViewerRecipeData));
        }
    }
}