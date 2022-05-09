using System.Collections.Generic;

namespace MainGame.UnityView.UI.Inventory.View.SubInventory.Element
{
    public class SubInventoryViewData
    {
        public List<OneSlot> OneSlots;
        public List<ArraySlot> ArraySlots;
        
        public List<ISubInventoryElement> Elements
        {
            get
            {
                var list = new List<ISubInventoryElement>();
                list.AddRange(OneSlots);
                list.AddRange(ArraySlots);
                list.Sort((a,b) => b.Priority - a.Priority);
                return list;
            }
        }

        public SubInventoryViewData(List<OneSlot> oneSlots, List<ArraySlot> arraySlots)
        {
            OneSlots = oneSlots;
            ArraySlots = arraySlots;
        }
    }
}