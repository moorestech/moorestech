using System;
using MainGame.UnityView.UI.Builder.Element;
using UnityEngine;
using UnityEngine.UI;

namespace MainGame.UnityView.UI.Builder.Unity
{
    public class UIBuilderButtonObject : MonoBehaviour, IUIBuilderObject
    {
        [SerializeField] private Button Button;

        private void Awake()
        {
            Button.onClick.AddListener(() => OnClick?.Invoke());
        }

        public IUIBluePrintElement BluePrintElement { get; private set; }
        public RectTransform RectTransform { get; private set; }

        public void Initialize(IUIBluePrintElement bluePrintElement)
        {
            RectTransform = GetComponent<RectTransform>();
            BluePrintElement = bluePrintElement;
        }

        public event Action OnClick;
    }
}