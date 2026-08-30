using System;
using System.Collections.Generic;
using Game.Block.Interface;
using Game.Block.Interface.Extension;
using Game.Context;
using Game.Gear.Common;
using Game.World.Interface.DataStore;
using Mooresmaster.Model.ChallengesModule;
using UniRx;

namespace Game.Challenge.Task
{
    /// <summary>
    ///     対象ブロックが接続先種別へ歯車接続した時に達成する。回転（RPM）は見ない
    ///     Completes when the target block gear-connects to the target kind; RPM is never inspected
    /// </summary>
    public class GearConnectToBlockChallengeTask : IChallengeTask
    {
        public ChallengeMasterElement ChallengeMasterElement { get; }
        public IObservable<IChallengeTask> OnChallengeComplete => _onChallengeComplete;
        private readonly Subject<IChallengeTask> _onChallengeComplete = new();
        
        private bool _completed;
        private bool _initialCollectDone;
        
        // 完了後にイベントを受け続けないよう購読を持ち、達成した瞬間に切る
        // Hold the subscriptions so events stop arriving the moment the challenge completes
        private readonly CompositeDisposable _blockEventSubscriptions = new();
        
        // 接続は設置と別ティックで確定するため、対象ブロックを溜めて毎ティック接続先を見る
        // Connections settle on a tick after placement, so keep target blocks and read their connects every tick
        private readonly HashSet<IBlock> _targetBlocks = new();
        
        private readonly Guid _targetBlockGuid;
        private readonly Guid _connectedBlockGuid;
        
        public static IChallengeTask Create(ChallengeMasterElement challengeMasterElement)
        {
            return new GearConnectToBlockChallengeTask(challengeMasterElement);
        }
        
        private GearConnectToBlockChallengeTask(ChallengeMasterElement challengeMasterElement)
        {
            ChallengeMasterElement = challengeMasterElement;
            
            var param = (GearConnectToBlockTaskParam)challengeMasterElement.TaskParam;
            _targetBlockGuid = param.BlockGuid;
            _connectedBlockGuid = param.ConnectedBlockGuid;
            
            _blockEventSubscriptions.Add(ServerContext.WorldBlockUpdateEvent.OnBlockPlaceEvent.Subscribe(OnBlockPlace));
            _blockEventSubscriptions.Add(ServerContext.WorldBlockUpdateEvent.OnBlockRemoveEvent.Subscribe(OnBlockRemove));
        }
        
        public void ManualUpdate()
        {
            if (_completed) return;
            
            CollectExistingBlocksOnce();
            
            foreach (var block in _targetBlocks)
            {
                if (!IsConnectedToTargetKind(block)) continue;
                _completed = true;
                break;
            }
            
            if (_completed)
            {
                _blockEventSubscriptions.Dispose();
                _targetBlocks.Clear();
                _onChallengeComplete.OnNext(this);
            }
            
            #region Internal
            
            // チャレンジ開始前から置かれていた対象ブロックを初回ティックで回収する
            // Collect target blocks placed before this challenge started, on the first tick
            void CollectExistingBlocksOnce()
            {
                if (_initialCollectDone) return;
                _initialCollectDone = true;
                foreach (var data in ServerContext.WorldBlockDatastore.BlockMasterDictionary.Values)
                {
                    if (data.Block.BlockGuid == _targetBlockGuid) _targetBlocks.Add(data.Block);
                }
            }
            
            // 1ホップの歯車接続相手に接続先種別が居るかを見る
            // Look for the target kind among the one-hop gear connections
            bool IsConnectedToTargetKind(IBlock block)
            {
                if (!block.TryGetComponent<IGearEnergyTransformer>(out var transformer)) return false;
                foreach (var connect in transformer.GetGearConnects())
                {
                    var connectedBlock = ServerContext.WorldBlockDatastore.GetBlock(connect.Transformer.BlockInstanceId);
                    if (connectedBlock != null && connectedBlock.BlockGuid == _connectedBlockGuid) return true;
                }
                return false;
            }
            
            #endregion
        }
        
        private void OnBlockPlace(BlockPlaceProperties properties)
        {
            if (_completed) return;
            
            var block = properties.BlockData.Block;
            if (block.BlockGuid == _targetBlockGuid) _targetBlocks.Add(block);
        }
        
        private void OnBlockRemove(BlockRemoveProperties properties)
        {
            if (_completed) return;
            
            var block = properties.BlockData.Block;
            if (block.BlockGuid == _targetBlockGuid) _targetBlocks.Remove(block);
        }
    }
}
