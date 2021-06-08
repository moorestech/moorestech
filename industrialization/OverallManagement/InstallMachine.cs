using System;
using industrialization.Core.Installation;
using industrialization.Core.Installation.Machine;
using industrialization.Core.Installation.Machine.util;

namespace industrialization.OverallManagement
{
    public class InstallMachine
    {
        public static NormalMachine Create(int id,int x,int y, Guid from, Guid to)
        {
            //機械の生成
            var machine = NormalMachineFactory.Create(id,Guid.NewGuid(), WorldInstallationInventoryDatastore.GetInstallation(to));
            
            //機械のコネクターを変更する
            machine.ChangeConnector(WorldInstallationInventoryDatastore.GetInstallation(from));
            WorldInstallationInventoryDatastore.GetInstallation(to).ChangeConnector(machine);
            
            //TODO ワールドデータに登録を別のところに
            WorldInstallationInventoryDatastore.AddInstallation(machine,machine.Guid);
            WorldInstallationDatastore.AddInstallation(machine,x,y);

            return machine;
        }
    }
}