using System.Collections.Generic;

namespace MainGame.UnityView.UI.Inventory.View.SubInventory
{
    public interface ISubInventoryElement
    {
        public SubInventoryElementType ElementType { get; }
        public int Priority { get; }
    }
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

    public class OneSlot : ISubInventoryElement
    {
        public SubInventoryElementType ElementType => SubInventoryElementType.OneSlot;
        public int Priority => priority;
        
        //表示するX座標
        public float X;
        //表示するY座標
        public float Y;
        //表示する順番
        public int priority;

        public InventorySlotElementOptions Options;

        public OneSlot(float x, float y, int priority, InventorySlotElementOptions options)
        {
            X = x;
            Y = y;
            this.priority = priority;
            Options = options;
        }
    }

    public class ArraySlot : ISubInventoryElement
    {
        public ArraySlot(float x, float y, int priority, int height, int width)
        {
            X = x;
            Y = y;
            this.priority = priority;
            Height = height;
            Width = width;
        }

        public SubInventoryElementType ElementType => SubInventoryElementType.ArraySlot;
        public int Priority => priority;
        
        public float X;
        public float Y;
        public int priority;
        
        public int Height;
        public int Width;
    }
    public enum SubInventoryElementType{
        OneSlot,
        ArraySlot
    }
}