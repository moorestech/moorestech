using System.Collections.Generic;
using MainGame.UnityView.UI.Builder.Element;
using UnityEngine;

namespace MainGame.UnityView.UI.Builder.Unity
{
    public class UIBuilderItemSlotArrayObject : MonoBehaviour, IUIBuilderObject
    {
        [SerializeField] private UIBuilderItemSlotObject UIBuilderItemSlotObject;

        public IUIBluePrintElement BluePrintElement { get; private set; }
        public RectTransform RectTransform { get; private set; }

        public void Initialize(IUIBluePrintElement bluePrintElement)
        {
            RectTransform = GetComponent<RectTransform>();
            BluePrintElement = bluePrintElement;
        }

        public List<UIBuilderItemSlotObject> SetArraySlot(int height, int weight, int bottomBlank)
        {
            var slots = new List<UIBuilderItemSlotObject>();
            for (var i = 0; i < height * weight - bottomBlank; i++)
            {
                var item = Instantiate(UIBuilderItemSlotObject, transform);
                slots.Add(item);
                item.Initialize(BluePrintElement);
            }

            return slots;
        }
    }
}