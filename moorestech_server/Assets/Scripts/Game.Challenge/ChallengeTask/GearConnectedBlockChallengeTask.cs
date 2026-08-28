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
    ///     指定ブロックが回り出した（RPMが正になった）時に達成する。接続の有無そのものは見ない
    ///     Completes once the target block starts spinning (RPM turns positive); the connection itself is never inspected
    /// </summary>
    public class GearConnectedBlockChallengeTask : IChallengeTask
    {
        public ChallengeMasterElement ChallengeMasterElement { get; }
        public IObservable<IChallengeTask> OnChallengeComplete => _onChallengeComplete;
        private readonly Subject<IChallengeTask> _onChallengeComplete = new();

        private bool _completed;
        private bool _initialCollectDone;

        // 完了後にイベントを受け続けないよう購読を持ち、達成した瞬間に切る
        // Hold the subscriptions so events stop arriving the moment the challenge completes
        private readonly CompositeDisposable _blockEventSubscriptions = new();

        // 回転は設置と無関係なティックで始まるため、対象ブロックを溜めて毎ティックRPMを見る
        // 同一ブロックの再登録と、撤去時に別インスタンスを落とす事故を防ぐため集合で持つ
        // Rotation starts on a tick unrelated to placement, so keep the target blocks and read RPM every tick
        // A set prevents both duplicate registration and removing the wrong instance on block removal
        private readonly HashSet<IBlock> _targetBlocks = new();

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

            _blockEventSubscriptions.Add(ServerContext.WorldBlockUpdateEvent.OnBlockPlaceEvent.Subscribe(OnBlockPlace));
            _blockEventSubscriptions.Add(ServerContext.WorldBlockUpdateEvent.OnBlockRemoveEvent.Subscribe(OnBlockRemove));
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

            bool IsSpinning(IBlock block)
            {
                if (!block.TryGetComponent<IGearEnergyTransformer>(out var transformer)) return false;
                return 0 < transformer.CurrentRpm.AsPrimitive();
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
