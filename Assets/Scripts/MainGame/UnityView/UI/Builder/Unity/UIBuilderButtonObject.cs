using System;
using UnityEngine;
using UnityEngine.UI;

namespace MainGame.UnityView.UI.Builder.Unity
{
    public class UIBuilderButton : MonoBehaviour
    {
        [SerializeField] private RectTransform RectTransform;
        [SerializeField] private Button Button;
        
        public event Action OnClick;

        private void Awake()
        {
            Button.onClick.AddListener(() => OnClick?.Invoke());
        }

        
        
    }
}