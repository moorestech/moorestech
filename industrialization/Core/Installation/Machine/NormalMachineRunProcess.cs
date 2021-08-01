using System;
using industrialization.Core.Config.Recipe.Data;
using industrialization.Core.Electric;
using industrialization.Core.GameSystem;

namespace industrialization.Core.Installation.Machine
{
    public class NormalMachineRunProcess : IUpdate,IInstallationElectric
    {
        //TODO コンフィグに必要電力量を追加
        private const int requestPower = 100;
        
        private IMachineRecipeData _machineRecipeData = new NullMachineRecipeData();
        public readonly NormalMachineOutputInventory NormalMachineOutputInventory;
        private int _nowPower = 0;
        private ProcessState _state = ProcessState.Idle;
        public NormalMachineRunProcess(NormalMachineOutputInventory normalMachineOutputInventory)
        {
            NormalMachineOutputInventory = normalMachineOutputInventory;
            GameUpdate.AddUpdateObject(this);
        }

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

        //TODO インプットスロット的に処理が始められそうだったら処理開始処理をだす
        public bool IsAllowedToStartProcess =>
            _state == ProcessState.Idle && NormalMachineOutputInventory.IsAllowedToOutputItem(_machineRecipeData);

        private double _milliSecondsRemaining = 0;
        private void Idle()
        {
            if (IsAllowedToStartProcess)
            {
                _state = ProcessState.Processing;
                _milliSecondsRemaining = _machineRecipeData.Time;
            }
        }
        private void Processing()
        {
            _milliSecondsRemaining -= GameUpdate.UpdateTime;
            Console.WriteLine(GameUpdate.UpdateTime);
            if(_milliSecondsRemaining <= 0) _state = ProcessState.ProcessingExit;
        }
        private void ProcessingExit()
        {
            
        }


        public void StartProcess(IMachineRecipeData _machineRecipeData)
        {
            if (_state == ProcessState.Idle)
            {
                this._machineRecipeData = _machineRecipeData;
                _state = ProcessState.Processing;
            }
        }
        
        
        
        public int RequestPower(){return requestPower;}
        public void SupplyPower(int power){_nowPower = power;}
    }
}