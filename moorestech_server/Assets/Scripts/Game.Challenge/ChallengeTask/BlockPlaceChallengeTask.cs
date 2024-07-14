using System;
using Game.Context;
using Game.Crafting.Interface;
using Game.World;
using Game.World.Interface.DataStore;
using UniRx;

namespace Game.Challenge.Task
{
    public class BlockPlaceChallengeTask : IChallengeTask
    {
        public ChallengeInfo Config { get; }
        public int PlayerId { get; }
        
        public IObservable<IChallengeTask> OnChallengeComplete => _onChallengeComplete;
        private readonly Subject<IChallengeTask> _onChallengeComplete = new();
        
        private bool _completed;
        
        public static IChallengeTask Create(int playerId, ChallengeInfo config)
        {
            return new BlockPlaceChallengeTask(playerId, config);
        }
        public BlockPlaceChallengeTask(int playerId, ChallengeInfo config)
        {
            Config = config;
            PlayerId = playerId;
            
            var worldEvent = ServerContext.WorldBlockUpdateEvent;
            worldEvent.OnBlockPlaceEvent.Subscribe(OnBlockPlace);
        }
        
        private void OnBlockPlace(BlockUpdateProperties properties)
        {
            if (_completed) return;
            
            var param = (BlockPlaceTaskParam)Config.TaskParam;
            
            if (param.BlockId == properties.BlockData.Block.BlockId)
            {
                _completed = true;
                _onChallengeComplete.OnNext(this);
            }
        }
        
        public void ManualUpdate()
        {
            
        }
    }
}