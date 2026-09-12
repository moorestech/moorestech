using System;
using System.Collections.Generic;
using Game.Block.Interface.Component;

namespace Game.Block.Blocks.TrainRail
{
    public class TrainStationComponent : IBlockSaveState
    {
        public string StationName { get; }
        public string SaveKey { get; } = typeof(TrainStationComponent).FullName;
        
        public object GetSaveState()
        {
            return new TrainStationComponentSaveData(StationName);
        }
        
        public TrainStationComponent(string stationName)
        {
            StationName = stationName;
        }
        
        public TrainStationComponent(Dictionary<string, object> componentStates) : this("test")
        {
            var saveData = BlockComponentStateReader.Read<TrainStationComponentSaveData>(componentStates, SaveKey);
            if (saveData == null) return;
            
            StationName = saveData.stationName;
        }
        
        public bool IsDestroy { get; private set; }
        
        public void Destroy()
        {
            IsDestroy = true;
        }
        
        [Serializable]
        public class TrainStationComponentSaveData
        {
            public string stationName;
            
            public TrainStationComponentSaveData(string stationName)
            {
                this.stationName = stationName;
            }
        }
    }
}