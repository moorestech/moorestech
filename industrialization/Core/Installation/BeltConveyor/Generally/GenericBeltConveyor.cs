using System;
using industrialization.Core.Installation.BeltConveyor.Interface;
using industrialization.Core.Item;

namespace industrialization.Core.Installation.BeltConveyor.Generally
{
    public class GenericBeltConveyor : InstallationBase, IInstallationInventory
    {
        public const int CanCarryItemNum = 1; 
        
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

        public void ChangeConnector(IInstallationInventory installationInventory)
        {
            _beltConveyorItemInventory.ChangeConnector(installationInventory);
        }

        public GenericBeltConveyor(int installationId, int intID,IBeltConveyorComponent beltConveyorItemInventory) : base(installationId, intID)
        {
            _beltConveyorItemInventory = beltConveyorItemInventory;
        }
    }
}