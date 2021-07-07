using System;
using industrialization.Core;
using industrialization.Core.Installation;
using industrialization.Core.Installation.Machine;
using industrialization.Core.Installation.Machine.util;
using industrialization.OverallManagement.DataStore;

namespace industrialization.OverallManagement
{
    public class InstallMachine
    {
        public static NormalMachine Create(int id,int x,int y, int inputId, int outputId)
        {
            //機械の生成
            var machine = NormalMachineFactory.Create(id,IntId.NewIntId(), WorldInstallationInventoryDatastore.GetInstallation(outputId));
            
            //機械のコネクターを変更する
            machine.ChangeConnector(WorldInstallationInventoryDatastore.GetInstallation(inputId));
            WorldInstallationInventoryDatastore.GetInstallation(outputId).ChangeConnector(machine);
            
            //TODO ワールドデータに登録を別のところに
            WorldInstallationInventoryDatastore.AddInstallation(machine,machine.IntId);
            WorldInstallationDatastore.AddInstallation(machine,x,y);

            return machine;
        }
    }
}