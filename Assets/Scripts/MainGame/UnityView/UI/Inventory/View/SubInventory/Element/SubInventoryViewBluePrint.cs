using System.Collections.Generic;

namespace MainGame.UnityView.UI.Inventory.View.SubInventory.Element
{
    public class SubInventoryViewBluePrint
    {
        private readonly List<OneSlot> _oneSlots;
        private readonly List<ArraySlot> _arraySlots;
        
        public List<ISubInventoryElement> Elements
        {
            get
            {
                var list = new List<ISubInventoryElement>();
                list.AddRange(_oneSlots);
                list.AddRange(_arraySlots);
                list.Sort((a,b) => b.Priority - a.Priority);
                return list;
            }
        }

        public SubInventoryViewBluePrint(List<OneSlot> oneSlots, List<ArraySlot> arraySlots)
        {
            _oneSlots = oneSlots;
            _arraySlots = arraySlots;
        }
    }
}