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

        // 駅名未設定は空文字で表す。表示名の既定はクライアント側が決める
        // An unnamed station is the empty string; the client decides the fallback display name
        private const string UnnamedStationName = "";

        public TrainStationComponent()
        {
            StationName = UnnamedStationName;
        }

        public TrainStationComponent(string stationName)
        {
            StationName = stationName;
        }

        public TrainStationComponent(Dictionary<string, object> componentStates) : this()
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
