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
    ///     歯車ブロックの状態で達成するチャレンジ。gearSpinning=対象が回り出した時（接続は見ない）、gearConnectedTo=接続先種別へ歯車接続した時（回転は見ない）
    ///     Gear-block state challenge: gearSpinning completes when the target starts turning (connection ignored), gearConnectedTo when it gear-connects to the target kind (RPM ignored)
    /// </summary>
    public class GearBlockChallengeTask : IChallengeTask
    {
        public ChallengeMasterElement ChallengeMasterElement { get; }
        public IObservable<IChallengeTask> OnChallengeComplete => _onChallengeComplete;
        private readonly Subject<IChallengeTask> _onChallengeComplete = new();
        
        private bool _completed;
        private bool _initialCollectDone;
        
        // 完了後にイベントを受け続けないよう購読を持ち、達成した瞬間に切る
        // Hold the subscriptions so events stop arriving the moment the challenge completes
        private readonly CompositeDisposable _blockEventSubscriptions = new();
        
        // 回転・接続とも設置と別ティックで確定するため、対象ブロックを溜めて毎ティック判定する
        // 同一ブロックの再登録と、撤去時に別インスタンスを落とす事故を防ぐため集合で持つ
        // Both spinning and connection settle on ticks after placement, so keep target blocks and judge every tick
        // A set prevents both duplicate registration and removing the wrong instance on block removal
        private readonly HashSet<IBlock> _targetBlocks = new();
        
        private readonly CompletionMode _mode;
        private readonly Guid _targetBlockGuid;
        private readonly Guid _connectedBlockGuid;
        
        private enum CompletionMode
        {
            Spinning,
            ConnectedTo,
        }
        
        public static IChallengeTask Create(ChallengeMasterElement challengeMasterElement)
        {
            return new GearBlockChallengeTask(challengeMasterElement);
        }
        
        private GearBlockChallengeTask(ChallengeMasterElement challengeMasterElement)
        {
            ChallengeMasterElement = challengeMasterElement;
            
            // 完了モードはTaskParamの型で決まる
            // The completion mode derives from the TaskParam type
            switch (challengeMasterElement.TaskParam)
            {
                case GearSpinningTaskParam spinning:
                    _mode = CompletionMode.Spinning;
                    _targetBlockGuid = spinning.BlockGuid;
                    break;
                case GearConnectedToTaskParam connectedTo:
                    _mode = CompletionMode.ConnectedTo;
                    _targetBlockGuid = connectedTo.BlockGuid;
                    _connectedBlockGuid = connectedTo.ConnectedBlockGuid;
                    break;
                default:
                    throw new InvalidOperationException($"Unsupported gear challenge TaskParam: {challengeMasterElement.TaskParam?.GetType().Name}");
            }
            
            _blockEventSubscriptions.Add(ServerContext.WorldBlockUpdateEvent.OnBlockPlaceEvent.Subscribe(OnBlockPlace));
            _blockEventSubscriptions.Add(ServerContext.WorldBlockUpdateEvent.OnBlockRemoveEvent.Subscribe(OnBlockRemove));
        }
        
        public void ManualUpdate()
        {
            if (_completed) return;
            
            CollectExistingBlocksOnce();
            
            foreach (var block in _targetBlocks)
            {
                if (!IsSatisfied(block)) continue;
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
            
            bool IsSatisfied(IBlock block)
            {
                if (!block.TryGetComponent<IGearEnergyTransformer>(out var transformer)) return false;
                
                // Spinning=RPMが正になった時、ConnectedTo=1ホップの接続相手に対象種別が居る時
                // Spinning: RPM turned positive. ConnectedTo: the target kind sits among one-hop gear connections
                if (_mode == CompletionMode.Spinning) return 0 < transformer.CurrentRpm.AsPrimitive();
                
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
