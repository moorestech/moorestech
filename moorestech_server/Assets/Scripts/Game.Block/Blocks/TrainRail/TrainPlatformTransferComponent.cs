using System;
using System.Collections.Generic;
using Game.Block.Interface.Component;
using Newtonsoft.Json;

namespace Game.Block.Blocks.TrainRail
{
    public class TrainPlatformTransferComponent : IBlockSaveState
    {
        public bool IsDestroy { get; private set; }
        public string SaveKey { get; } = typeof(TrainPlatformTransferComponent).FullName;
        public TransferMode Mode { get; private set; }
        
        public TrainPlatformTransferComponent(TransferMode mode)
        {
            Mode = mode;
        }
        
        public TrainPlatformTransferComponent(Dictionary<string, string> componentStates)
        {
            var serialized = componentStates[SaveKey];
            var saveData = JsonConvert.DeserializeObject<TrainPlatformTransferComponentSaveData>(serialized);
            if (saveData == null) return;
            
            Mode = saveData.mode;
        }
        
        public void SetMode(TransferMode mode)
        {
            Mode = mode;
        }
        
        public void Destroy()
        {
            IsDestroy = true;
        }

        public string GetSaveState()
        {
            return JsonConvert.SerializeObject(new TrainPlatformTransferComponentSaveData(Mode));
        }
        
        public enum TransferMode
        {
            LoadToTrain,
            UnloadToPlatform,
        }
        
        [Serializable]
        private class TrainPlatformTransferComponentSaveData
        {
            public TransferMode mode;
            
            public TrainPlatformTransferComponentSaveData(TransferMode mode)
            {
                this.mode = mode;
            }
        }
    }
}