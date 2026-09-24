using System;
using System.Collections.Generic;
using Game.Block.Interface.Component;
using UniRx;

namespace Game.Block.Blocks.TrainRail
{
    public class TrainStationComponent : IBlockSaveState, IBlockStateObservable
    {
        public string StationName { get; private set; }
        public string SaveKey { get; } = typeof(TrainStationComponent).FullName;
        public bool IsDestroy { get; private set; }

        // 駅名変更をブロック状態の購読者へ通知する
        // Notify block state subscribers when the station name changes
        private readonly Subject<Unit> _onChangeBlockState = new();
        public IObservable<Unit> OnChangeBlockState => _onChangeBlockState;

        public TrainStationComponent(string stationName)
        {
            StationName = stationName;
        }

        public TrainStationComponent(Dictionary<string, object> componentStates) : this(string.Empty)
        {
            if (!BlockComponentStateReader.TryRead<TrainStationComponentSaveData>(componentStates, SaveKey, out var saveData)) return;
            StationName = saveData.stationName;
        }

        public void SetStationName(string stationName)
        {
            StationName = stationName;
            _onChangeBlockState.OnNext(Unit.Default);
        }

        public BlockStateDetail[] GetBlockStateDetails()
        {
            return new[] { TrainStationNameStateDetail.CreateState(StationName) };
        }
        
        public object GetSaveState()
        {
            return new TrainStationComponentSaveData(StationName);
        }
        
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
