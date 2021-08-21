using System;
using industrialization.Core;
using industrialization.Core.Block;
using industrialization.Core.Block.Machine;
using industrialization.Core.Block.Machine.util;
using industrialization.OverallManagement.DataStore;

namespace industrialization.OverallManagement
{
    public class InstallMachine
    {
        public static NormalMachine Create(uint id,int x,int y, uint inputId, uint outputId)
        {
            //機械の生成
            var machine = NormalMachineFactory.Create(id,IntId.NewIntId(), WorldBlockInventoryDatastore.GetBlock(outputId));
            
            //機械のコネクターを変更する
            machine.ChangeConnector(WorldBlockInventoryDatastore.GetBlock(inputId));
            WorldBlockInventoryDatastore.GetBlock(outputId).ChangeConnector(machine);
            
            //TODO ワールドデータに登録を別のところに
            WorldBlockInventoryDatastore.AddBlock(machine,machine.IntId);
            WorldBlockDatastore.AddBlock(machine,x,y);

            return machine;
        }
    }
}