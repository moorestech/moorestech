using System;
using System.Threading;
using Client.Common;
using Client.Game.InGame.Block;
using Client.Game.InGame.BlockSystem;
using Client.Game.InGame.Tutorial.TutorialBlock;
using Client.Game.InGame.UI.UIState;
using Core.Master;
using Cysharp.Threading.Tasks;
using Game.Block.Interface;
using Game.Block.Interface.Extension;
using Mooresmaster.Model.ChallengesModule;
using UniRx;
using UnityEngine;
using VContainer;

namespace Client.Game.InGame.Tutorial
{
    /// <summary>
    ///     設置目標セルのゴーストを1体だけ持ち、その生成・移動・破棄を引き受ける。どのセルを指すかは呼び手が決める
    ///     Owns the single target-cell ghost and its creation, movement and teardown; which cell to point at is the caller's decision
    /// </summary>
    public class BlockPlacePreviewTutorialManager : MonoBehaviour, ITutorialView, ITutorialViewManager
    {
        // WebオーバーレイでのピンID。BlockPlacePreviewTutorialManagerはシーンに1つなので固定IDでよい
        // World-pin id on the web overlay; a single scene instance suffices, so the id is fixed
        private const string WebPinId = "block-place-preview-pin";

        public string TutorialType => TutorialsElement.TutorialTypeConst.blockPlacePreview;

        private BlockGameObjectDataStore _blockGameObjectDataStore;
        private TutorialBlockPreviewObject _previewObject;
        private BlockId _previewObjectBlockId;

        private BlockId _targetBlockId;
        private Vector3Int? _targetCell;
        private BlockDirection _targetDirection;
        private string _pinTutorialGuid = "";

        private IDisposable _blockPlacedDisposable;

        // ゴースト生成はAddressableロードを挟むため、await明けに古い対象へ書き戻さないようトークンで打ち切る
        // Ghost creation awaits an Addressable load, so a token cancels it instead of writing back to a stale target
        private CancellationTokenSource _previewCancellation;

        [Inject]
        public void Construct(BlockGameObjectDataStore blockGameObjectDataStore)
        {
            _blockGameObjectDataStore = blockGameObjectDataStore;
        }

        /// <summary>
        ///     ゴーストを出す目標セルを差し替える。同じ目標なら何もしないので毎フレーム呼んでよい
        ///     Replaces the target cell the ghost points at; an unchanged target is a no-op, so it is safe to call every frame
        /// </summary>
        public void SetTargetCell(BlockId blockId, Vector3Int cell, BlockDirection direction, string tutorialGuid)
        {
            _pinTutorialGuid = tutorialGuid;
            if (_targetCell == cell && _targetBlockId == blockId && _targetDirection == direction) return;

            _targetBlockId = blockId;
            _targetCell = cell;
            _targetDirection = direction;

            CancelPendingPreview();
            _previewCancellation = new CancellationTokenSource();
            ShowPreviewAsync(_previewCancellation.Token).Forget();
        }

        public void ClearTarget()
        {
            CancelPendingPreview();
            _targetCell = null;
            if (_previewObject != null) _previewObject.SetActive(false);

            // Webピンは冪等に除去（未配信でも安全）
            // Removing the web pin is idempotent, safe even when never published
            WorldPinStateStore.Instance.RemovePin(WebPinId);
        }

        private void Update()
        {
            // Webへ射影配信する（3Dプレビュー自体はUnity側に残置し、矢印/ピンのみWeb化）
            // Project and publish to the web overlay (the 3D preview stays in Unity; only the arrow/pin moves to web)
            if (!WebUiScreenGate.IsWebUiMode || _targetCell == null || _previewObject == null) return;

            var camera = CameraManager.MainCamera.Camera;
            if (!camera) return;

            var projection = WorldPinScreenProjection.Project(camera, _previewObject.transform.position);
            WorldPinStateStore.Instance.SetPin(WebPinId, _pinTutorialGuid, projection);
        }

        public ITutorialView ApplyTutorial(TutorialsElement tutorial)
        {
            var param = (BlockPlacePreviewTutorialParam)tutorial.TutorialParam;
            var blockId = MasterHolder.BlockMaster.GetBlockId(param.BlockGuid);
            var direction = Enum.Parse<BlockDirection>(param.BlockDirection);

            // 既に目標ブロックが配置済みなら早期終了
            // Exit early when the target block already exists
            if (IsTargetBlockPlaced()) return null;

            SetTargetCell(blockId, param.Position, direction, tutorial.TutorialGuid.ToString("D"));
            SubscribePlacementEvent();

            return this;

            #region Internal

            bool IsTargetBlockPlaced()
            {
                return _blockGameObjectDataStore.TryGetBlockGameObject(param.Position, out var block) && block.BlockId == blockId;
            }

            // 指定座標への対象ブロック設置で完了する
            // Completes when the target block lands on the specified position
            void SubscribePlacementEvent()
            {
                _blockPlacedDisposable?.Dispose();
                _blockPlacedDisposable = _blockGameObjectDataStore.OnBlockPlaced.Subscribe(block =>
                {
                    if (block.BlockId != blockId) return;
                    if (block.BlockPosInfo.OriginalPos != param.Position) return;

                    CompleteTutorial();
                });
            }

            #endregion
        }

        public void CompleteTutorial()
        {
            _blockPlacedDisposable?.Dispose();
            _blockPlacedDisposable = null;
            ClearTarget();
        }

        private async UniTaskVoid ShowPreviewAsync(CancellationToken cancellationToken)
        {
            if (_previewObject == null || _previewObjectBlockId != _targetBlockId)
            {
                if (_previewObject != null) _previewObject.DestroyPreview();

                var created = await TutorialPreviewBlockCreator.CreateAsync(_targetBlockId, cancellationToken);
                if (created == null) return;

                _previewObject = created;
                _previewObjectBlockId = _targetBlockId;
                _previewObject.transform.SetParent(transform);
            }

            var position = SlopeBlockPlaceSystem.GetBlockPositionToPlacePosition(_targetCell.Value, _targetDirection, _targetBlockId);
            _previewObject.SetTransform(position, _targetDirection.GetRotation());
            _previewObject.SetPlaceableColor(true);
            _previewObject.SetActive(true);
        }

        private void CancelPendingPreview()
        {
            if (_previewCancellation == null) return;

            _previewCancellation.Cancel();
            _previewCancellation.Dispose();
            _previewCancellation = null;
        }

        private void OnDestroy()
        {
            CancelPendingPreview();
            WorldPinStateStore.Instance.RemovePin(WebPinId);
        }
    }
}
