using System.Collections.Generic;
using UnityEngine;

namespace MainGame.UnityView.UI.Builder.Unity
{
    public class UIBuilderItemSlotArrayObject : MonoBehaviour
    {
        [SerializeField] private UIBuilderItemSlotObject UIBuilderItemSlotObject;
        public List<UIBuilderItemSlotObject> SetArraySlot(int height, int weight,int bottomBlank)
        {
            var slots = new List<UIBuilderItemSlotObject>();
            for (int i = 0; i < height * weight - bottomBlank; i++)
            {
                slots.Add(Instantiate(UIBuilderItemSlotObject, transform));
            }

            return slots;
        }
    }
}