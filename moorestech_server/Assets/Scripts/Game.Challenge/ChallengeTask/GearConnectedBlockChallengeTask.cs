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
    ///     指定ブロックが歯車ネットワークに繋がって実際に回り出した時に達成する
    ///     Completes when the target block is wired into a gear network and actually starts spinning
    /// </summary>
    public class GearConnectedBlockChallengeTask : IChallengeTask
    {
        public ChallengeMasterElement ChallengeMasterElement { get; }
        public IObservable<IChallengeTask> OnChallengeComplete => _onChallengeComplete;
        private readonly Subject<IChallengeTask> _onChallengeComplete = new();

        private bool _completed;
        private bool _initialCollectDone;

        // 回転は設置と無関係なティックで始まるため、対象ブロックを溜めて毎ティックRPMを見る
        // Rotation starts on a tick unrelated to placement, so keep the target blocks and read RPM every tick
        private readonly List<IBlock> _targetBlocks = new();

        private readonly Guid _targetBlockGuid;

        public static IChallengeTask Create(ChallengeMasterElement challengeMasterElement)
        {
            return new GearConnectedBlockChallengeTask(challengeMasterElement);
        }

        private GearConnectedBlockChallengeTask(ChallengeMasterElement challengeMasterElement)
        {
            ChallengeMasterElement = challengeMasterElement;

            var param = (GearConnectedBlockTaskParam)challengeMasterElement.TaskParam;
            _targetBlockGuid = param.BlockGuid;

            ServerContext.WorldBlockUpdateEvent.OnBlockPlaceEvent.Subscribe(OnBlockPlace);
            ServerContext.WorldBlockUpdateEvent.OnBlockRemoveEvent.Subscribe(OnBlockRemove);
        }

        public void ManualUpdate()
        {
            if (_completed) return;

            CollectExistingBlocksOnce();

            foreach (var block in _targetBlocks)
            {
                if (!IsSpinning(block)) continue;
                _completed = true;
                break;
            }

            if (_completed) _onChallengeComplete.OnNext(this);

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

            bool IsSpinning(IBlock block)
            {
                if (!block.TryGetComponent<IGearEnergyTransformer>(out var transformer)) return false;
                return 0 < transformer.CurrentRpm.AsPrimitive();
            }

            #endregion
        }

        private void OnBlockPlace(BlockPlaceProperties properties)
        {
            var block = properties.BlockData.Block;
            if (block.BlockGuid == _targetBlockGuid) _targetBlocks.Add(block);
        }

        private void OnBlockRemove(BlockRemoveProperties properties)
        {
            var block = properties.BlockData.Block;
            if (block.BlockGuid == _targetBlockGuid) _targetBlocks.Remove(block);
        }
    }
}
