using System;
using System.Collections.Generic;
using Game.Block.Interface;
using Game.Block.Interface.Extension;
using Game.Context;
using Game.World.Interface.DataStore;
using Mooresmaster.Model.ChallengesModule;
using UniRx;

namespace Game.Challenge.Task
{
    /// <summary>
    ///     指定ブロックが指定種別の鉱脈の上に置かれた時に達成する（底面フットプリントのXZ重なりで判定しYは見ない）
    ///     Completes when the block is placed over any vein of the type, judged by footprint XZ overlap with Y ignored
    /// </summary>
    public class BlockPlaceOnVeinChallengeTask : IChallengeTask
    {
        public ChallengeMasterElement ChallengeMasterElement { get; }
        public IObservable<IChallengeTask> OnChallengeComplete => _onChallengeComplete;
        private readonly Subject<IChallengeTask> _onChallengeComplete = new();

        private bool _completed;
        private bool _initialCheckDone;

        // 完了後にイベントを受け続けないよう購読を持ち、達成した瞬間に切る
        // Hold the subscription so events stop arriving the moment the challenge completes
        private IDisposable _blockPlaceSubscription;

        // イベントは判定対象ブロックを積むだけで、判定と発火はティックで行う（前例: EquipItemChallengeTask）
        // Events only enqueue blocks to check; the check and completion fire on the tick (precedent: EquipItemChallengeTask)
        private readonly List<IBlock> _blocksToCheck = new();

        private readonly Guid _targetBlockGuid;
        private readonly Guid _targetVeinTypeGuid;

        public static IChallengeTask Create(ChallengeMasterElement challengeMasterElement)
        {
            return new BlockPlaceOnVeinChallengeTask(challengeMasterElement);
        }

        private BlockPlaceOnVeinChallengeTask(ChallengeMasterElement challengeMasterElement)
        {
            ChallengeMasterElement = challengeMasterElement;

            var param = (BlockPlaceOnVeinTaskParam)challengeMasterElement.TaskParam;
            _targetBlockGuid = param.BlockGuid;
            _targetVeinTypeGuid = param.VeinGuid;

            _blockPlaceSubscription = ServerContext.WorldBlockUpdateEvent.OnBlockPlaceEvent.Subscribe(OnBlockPlace);
        }

        public void ManualUpdate()
        {
            if (_completed) return;

            EnqueueInitialCheckOnce();

            foreach (var block in _blocksToCheck)
            {
                if (!IsOverTargetVeinType(block)) continue;
                _completed = true;
                break;
            }
            _blocksToCheck.Clear();

            if (_completed)
            {
                _blockPlaceSubscription.Dispose();
                _onChallengeComplete.OnNext(this);
            }

            #region Internal

            // チャレンジ開始前から置かれていた対象ブロックを初回ティックで回収する
            // Recover target blocks placed before this challenge started, on the first tick
            void EnqueueInitialCheckOnce()
            {
                if (_initialCheckDone) return;
                _initialCheckDone = true;
                foreach (var data in ServerContext.WorldBlockDatastore.BlockMasterDictionary.Values)
                {
                    if (data.Block.BlockGuid == _targetBlockGuid) _blocksToCheck.Add(data.Block);
                }
            }

            // 判定規則の正本は BlockPositionInfoExtension.OverlapsVeinXz。クライアントの設置制限と同じ1本を呼ぶ
            // BlockPositionInfoExtension.OverlapsVeinXz owns the rule; this calls the very same one the client placement restriction uses
            bool IsOverTargetVeinType(IBlock block)
            {
                if (block.BlockGuid != _targetBlockGuid) return false;

                foreach (var vein in ServerContext.ItemMapVeinDatastore.Veins)
                {
                    if (vein.VeinGuid != _targetVeinTypeGuid) continue;
                    if (block.BlockPositionInfo.OverlapsVeinXz(vein.VeinRangeMin, vein.VeinRangeMax)) return true;
                }
                return false;
            }

            #endregion
        }

        private void OnBlockPlace(BlockPlaceProperties properties)
        {
            if (_completed) return;

            var block = properties.BlockData.Block;
            if (block.BlockGuid == _targetBlockGuid) _blocksToCheck.Add(block);
        }
    }
}
