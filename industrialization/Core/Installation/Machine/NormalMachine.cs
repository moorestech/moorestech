using System;
using System.Collections.Generic;
using System.Linq;
using industrialization.Core.Config.Installation;
using industrialization.Core.GameSystem;
using industrialization.Core.Item;
using industrialization.Core.Util;

namespace industrialization.Core.Installation.Machine
{
    //TODO アウトプットのほうもつくる
    public class NormalMachine : InstallationBase,IInstallationInventory,IUpdate
    {
        private readonly NormalMachineInputInventory _normalMachineInputInventory;
        private readonly NormalMachineOutputInventory _normalMachineOutputInventory;
        private ProcessState _state = ProcessState.Idle;
        
        public NormalMachine(int installationId, int intId,
            NormalMachineInputInventory normalMachineInputInventory,
            NormalMachineOutputInventory normalMachineOutputInventory) : base(installationId, intId)
        {
            _normalMachineInputInventory = normalMachineInputInventory;
            _normalMachineOutputInventory = normalMachineOutputInventory;
            intId = intId;
            InstallationID = installationId;
            GameUpdate.AddUpdateObject(this);
        }
        public IItemStack InsertItem(IItemStack itemStack)
        {
            return _normalMachineInputInventory.InsertItem(itemStack);
        }
        public void ChangeConnector(IInstallationInventory installationInventory)
        {
            _normalMachineOutputInventory.ChangeConnectInventory(installationInventory);
        }

        private bool IsAllowedToStartProcess =>
            _state == ProcessState.Idle && _normalMachineInputInventory.IsAllowedToStartProcess;

        public void Update()
        {
            switch (_state)
            {
                case ProcessState.Idle :
                    Idle();
                    break;
                case ProcessState.Processing :
                    Processing();
                    break;
                case ProcessState.ProcessingExit :
                    ProcessingExit();
                    break;
            }
        }
        private void Idle()
        {
            
        }
        private void Processing()
        {
            
        }
        private void ProcessingExit()
        {
            
        }
    }
}