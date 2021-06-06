using System;
using industrialization.Core.Installation;
using industrialization.Core.Installation.Machine.util;

namespace industrialization.OverallManagement
{
    public class InstallationMachine
    {
        public static void Create(int id,int x,int y, Guid from, Guid to)
        {
            //機械の生成
            var machine = NormalMachineFactory.Create(id,Guid.NewGuid(), WorldInstallationInventoryDatastore.GetInstallation(to));
            
            //機械のコネクターを変更する
            machine.ChangeConnector(WorldInstallationInventoryDatastore.GetInstallation(from));
            WorldInstallationInventoryDatastore.GetInstallation(to).ChangeConnector(machine);
            
            //ワールドデータに登録
            WorldInstallationInventoryDatastore.AddInstallation(machine,machine.Guid);
            WorldInstallationDatastore.AddInstallation(machine,x,y);
        }
    }
}