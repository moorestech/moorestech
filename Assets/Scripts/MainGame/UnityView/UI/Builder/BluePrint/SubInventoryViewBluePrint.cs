using System.Collections.Generic;

namespace MainGame.UnityView.UI.Builder.BluePrint
{
    public class SubInventoryViewBluePrint
    {
        public List<UIBluePrintItemSlot> OneSlots = new();
        public List<UIBluePrintItemSlotArray> ArraySlots = new();
        public List<TextElement> TextElements = new();

        public List<ISubInventoryElement> Elements
        {
            get
            {
                var list = new List<ISubInventoryElement>();
                list.AddRange(OneSlots);
                list.AddRange(ArraySlots);
                list.AddRange(TextElements);
                list.Sort((a,b) => b.Priority - a.Priority);
                return list;
            }
        }
    }
}