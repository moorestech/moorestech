using System.Collections.Generic;

namespace MainGame.UnityView.UI.Inventory.View.SubInventory.Element
{
    public class SubInventoryViewBluePrint
    {
        public List<OneSlot> OneSlots = new();
        public List<ArraySlot> ArraySlots = new();
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