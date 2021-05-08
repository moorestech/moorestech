using System.Collections.Generic;
using industrialization.Item;

namespace industrialization.Installation.Machine
{
    public interface IMachineComponent
    {
        public IItemStack InsertItem(IItemStack itemStacks);
    }
}