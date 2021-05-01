using System;
using industrialization.Installation.BeltConveyor.Interface;
using industrialization.Item;

namespace industrialization.Installation.BeltConveyor.Generally
{
    public class GenerallyBeltConveyor : InstallationBase, IInstallationInventory
    {
        private readonly IBeltConveyorComponent _beltConveyorItemInventory;
        private const int CanCarryItemNum = 1; 
        
        /// <summary>
        /// アイテムの搬入が出来たら指定個数減らしてアイテムを返す
        /// 搬入が出来なかったらそのままアイテムを返す
        /// </summary>
        /// <param name="itemStack">搬入したいアイテム</param>
        /// <returns>搬入の結果のアイテム</returns>
        public IItemStack InsertItem(IItemStack itemStack)
        {
            if (_beltConveyorItemInventory.InsertItem(itemStack))
            {
                return itemStack.SubItem(CanCarryItemNum);
            }
            return itemStack;
        }

        public GenerallyBeltConveyor(int installationId, Guid guid,IBeltConveyorComponent beltConveyorItemInventory) : base(installationId, guid)
        {
            _beltConveyorItemInventory = beltConveyorItemInventory;
        }
    }
}