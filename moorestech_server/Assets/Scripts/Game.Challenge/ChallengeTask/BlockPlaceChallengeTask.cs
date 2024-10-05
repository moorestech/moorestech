using System;
using Game.Context;
using Game.World.Interface.DataStore;
using Mooresmaster.Model.ChallengesModule;
using UniRx;

namespace Game.Challenge.Task
{
    public class BlockPlaceChallengeTask : IChallengeTask
    {
        public ChallengeMasterElement ChallengeMasterElement { get; }
        public int PlayerId { get; }
        
        public IObservable<IChallengeTask> OnChallengeComplete => _onChallengeComplete;
        private readonly Subject<IChallengeTask> _onChallengeComplete = new();
        
        private bool _completed;
        
        public static IChallengeTask Create(int playerId, ChallengeMasterElement challengeMasterElement)
        {
            return new BlockPlaceChallengeTask(playerId, challengeMasterElement);
        }
        public BlockPlaceChallengeTask(int playerId, ChallengeMasterElement challengeMasterElement)
        {
            ChallengeMasterElement = challengeMasterElement;
            PlayerId = playerId;
            
            var worldEvent = ServerContext.WorldBlockUpdateEvent;
            worldEvent.OnBlockPlaceEvent.Subscribe(OnBlockPlace);
        }
        
        private void OnBlockPlace(BlockUpdateProperties properties)
        {
            if (_completed) return;
            
            var param = ChallengeMasterElement.TaskParam as BlockPlaceTaskParam;
            if (param.BlockGuid == properties.BlockData.Block.BlockGuid)
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