using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Mooresmaster.Model.CraftRecipesModule;
using UniRx;

namespace Client.Game.InGame.UI.Inventory.Element
{
    public class CraftRecipeItemElement : MonoBehaviour
    {
        [SerializeField] private Image iconImage;
        [SerializeField] private TMP_Text recipeNameText;
        [SerializeField] private GameObject selectedHighlight;

        private CraftRecipeMasterElement _recipe;
        private IDisposable _clickDisposable;

        // 選択状態
        public bool IsSelected
        {
            get => selectedHighlight != null && selectedHighlight.activeSelf;
            set
            {
                if (selectedHighlight != null)
                    selectedHighlight.SetActive(value);
            }
        }

        // レシピデータをセット
        public void SetRecipe(CraftRecipeMasterElement recipe, Sprite icon, string recipeName)
        {
            _recipe = recipe;
            if (iconImage != null) iconImage.sprite = icon;
            if (recipeNameText != null) recipeNameText.text = recipeName;
        }

        // クリックイベント購読
        public void SetOnClick(Action<CraftRecipeItemElement> onClick)
        {
            if (_clickDisposable != null) _clickDisposable.Dispose();
            var button = GetComponent<Button>();
            if (button != null)
            {
                _clickDisposable = button.OnClickAsObservable()
                    .Subscribe(_ => onClick?.Invoke(this))
                    .AddTo(this);
            }
        }

        public CraftRecipeMasterElement GetRecipe()
        {
            return _recipe;
        }

        private void OnDestroy()
        {
            if (_clickDisposable != null) _clickDisposable.Dispose();
        }
    }
}