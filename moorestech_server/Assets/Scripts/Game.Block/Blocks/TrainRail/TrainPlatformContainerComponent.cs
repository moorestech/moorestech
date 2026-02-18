using System;
using System.Collections.Generic;
using Game.Block.Interface.Component;
using Game.Train.Unit;
using JetBrains.Annotations;
using Newtonsoft.Json;

namespace Game.Block.Blocks.TrainRail
{
    public class TrainPlatformContainerComponent : IBlockSaveState
    {
        [CanBeNull] public ITrainCarContainer Container;
        
        public bool IsDestroy { get; private set; }
        public string SaveKey { get; } = typeof(TrainPlatformContainerComponent).FullName;
        
        public TrainPlatformContainerComponent([CanBeNull] ITrainCarContainer container = null)
        {
            Container = container;
        }
        
        public TrainPlatformContainerComponent(Dictionary<string, string> componentStates) : this(container: null)
        {
        }
        
        public void Destroy()
        {
            IsDestroy = true;
        }
        
        public string GetSaveState()
        {
            return JsonConvert.SerializeObject(new TrainPlatformContainerComponentSaveData());
        }
        
        [Serializable]
        public class TrainPlatformContainerComponentSaveData
        {
        }
    }
}