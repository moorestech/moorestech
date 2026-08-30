using System;
using Client.Game.InGame.Block;
using Client.Game.InGame.Player;
using Core.Master;
using Game.Block.Interface;
using Mooresmaster.Model.ChallengesModule;
using UniRx;
using UnityEngine;
using VContainer;

namespace Client.Game.InGame.Tutorial.PlacementGuide
{
    /// <summary>
    ///     最寄りのアンカーブロック原点＋offset を目標セルとして押し出し、そこへ対象ブロックが置かれたら完了する
    ///     Pushes nearest-anchor origin + offset as the target cell and completes when the target block lands there
    ///     ゴーストそのものの生成と破棄は BlockPlacePreviewTutorialManager が持つ
    ///     BlockPlacePreviewTutorialManager owns creating and tearing down the ghost itself
    /// </summary>
    public class RelativeBlockPlacePreviewTutorialManager : MonoBehaviour, ITutorialView, ITutorialViewManager
    {
        public string TutorialType => TutorialsElement.TutorialTypeConst.relativeBlockPlacePreview;

        private BlockGameObjectDataStore _blockGameObjectDataStore;
        private BlockPlacePreviewTutorialManager _blockPlacePreviewTutorialManager;

        private RelativeBlockPlacePreviewTutorialParam _currentParam;
        private Guid _anchorBlockGuid;
        private BlockId _targetBlockId;
        private BlockDirection _direction;
        private Vector3Int? _targetCell;
        private IDisposable _blockPlacedDisposable;
        private string _pinTutorialGuid = "";

        [Inject]
        public void Construct(BlockGameObjectDataStore blockGameObjectDataStore, BlockPlacePreviewTutorialManager blockPlacePreviewTutorialManager)
        {
            _blockGameObjectDataStore = blockGameObjectDataStore;
            _blockPlacePreviewTutorialManager = blockPlacePreviewTutorialManager;
        }

        public ITutorialView ApplyTutorial(TutorialsElement tutorial)
        {
            _currentParam = (RelativeBlockPlacePreviewTutorialParam)tutorial.TutorialParam;
            _pinTutorialGuid = tutorial.TutorialGuid.ToString("D");
            _anchorBlockGuid = _currentParam.AnchorBlockGuid;
            _targetBlockId = MasterHolder.BlockMaster.GetBlockId(_currentParam.BlockGuid);
            _direction = Enum.Parse<BlockDirection>(_currentParam.BlockDirection);
            _targetCell = null;

            SubscribePlacementEvent();
            return this;

            #region Internal

            // 目標セルへの対象ブロック設置で完了する。セルはアンカー追従で動くため毎回現在値と突き合わせる
            // Completes when the target block lands on the target cell; the cell moves with the anchor, so it is compared each time
            void SubscribePlacementEvent()
            {
                _blockPlacedDisposable?.Dispose();
                _blockPlacedDisposable = _blockGameObjectDataStore.OnBlockPlaced.Subscribe(block =>
                {
                    if (block.BlockId != _targetBlockId) return;
                    if (_targetCell == null || block.BlockPosInfo.OriginalPos != _targetCell.Value) return;
                    CompleteTutorial();
                });
            }

            #endregion
        }

        public void CompleteTutorial()
        {
            _blockPlacedDisposable?.Dispose();
            _blockPlacedDisposable = null;
            _targetCell = null;
            _blockPlacePreviewTutorialManager.ClearTarget();
            _currentParam = null;
        }

        private void Update()
        {
            if (_currentParam == null) return;

            // アンカーは撤去や増設で変わるため毎フレーム最寄りを取り直す（VeinPin と同じ追従）
            // The anchor can be removed or duplicated, so re-pick the nearest one every frame (same tracking as VeinPin)
            var anchor = _blockGameObjectDataStore.SearchNearestBlock(_anchorBlockGuid, PlayerSystemContainer.Instance.PlayerObjectController.Position);
            if (anchor == null)
            {
                _targetCell = null;
                _blockPlacePreviewTutorialManager.ClearTarget();
                return;
            }

            // アンカーの向きで回したローカルセルを使う（gearConnectsと同じ換算）
            // Use the anchor-rotated local cell (same conversion as gearConnects)
            _targetCell = anchor.BlockPosInfo.ConvertBlockLocalToWorldCell(_currentParam.Offset);
            var worldDirection = AnchorRelativeDirectionUtil.RotateByAnchor(_direction, anchor.BlockPosInfo.BlockDirection);

            // アンカーが動いた先に既に対象ブロックがあれば、設置イベントは二度と来ないのでここで完了させる
            // When the target block already sits where the anchor moved to, no placement event will ever come, so complete here
            if (_blockGameObjectDataStore.TryGetBlockGameObject(_targetCell.Value, out var existing) && existing.BlockId == _targetBlockId)
            {
                CompleteTutorial();
                return;
            }

            _blockPlacePreviewTutorialManager.SetTargetCell(_targetBlockId, _targetCell.Value, worldDirection, _pinTutorialGuid);
        }
    }
}
