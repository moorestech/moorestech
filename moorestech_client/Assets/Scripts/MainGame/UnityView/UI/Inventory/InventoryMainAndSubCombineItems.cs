using System.Collections;
using System.Collections.Generic;
using Core.Item;

namespace MainGame.UnityView.UI.Inventory
{
    public interface IInventoryItems : IEnumerable<IItemStack>
    {
        public IItemStack this[int index] { get; }
    }
    
    public class InventoryMainAndSubCombineItems : IInventoryItems
    {
        public IEnumerator<IItemStack> GetEnumerator()
        {
            throw new System.NotImplementedException();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
        


        public IItemStack this[int index]
        {
            get
            {
                throw new System.NotImplementedException();
            }

            set
            {
                
            }
        }
    }
}