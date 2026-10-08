using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Client.Game.InGame.Block;
using Client.Game.InGame.BlockSystem.PlaceSystem.Common;
using Client.Game.InGame.BlockSystem.PlaceSystem.Common.Height;
using Client.Game.InGame.BlockSystem.PlaceSystem.Feedback;
using Client.Game.InGame.BlockSystem.PlaceSystem.Targets;
using Client.Game.InGame.BlockSystem.PlaceSystem.Util;
using Client.Game.InGame.Context;
using Client.Game.InGame.UI.Blueprint;
using Cysharp.Threading.Tasks;
using Game.Block.Interface;
using Mooresmaster.Model.BlocksModule;
using UniRx;
using UnityEngine;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.Blueprint.Copy
{
    /// <summary>
    ///     1クリックで始点、もう1クリックで終点を決めて名前入力へ進む
    ///     Picks start and end with one click each, then opens naming
    /// </summary>
    public class BlueprintCopySystem : PlaceSystemBase<BlueprintCopyPlacementTarget>
    {
        private readonly ClientBlueprintLibrary _library;
        private readonly BlueprintNameInputState _nameInputState;
        private readonly PlacementHeightOffset _heightOffset;
        private readonly BlockGameObjectDataStore _blockGameObjectDataStore;
        private readonly Camera _mainCamera;
        private readonly BlueprintCopySelection _selection = new();
        private readonly BlueprintCopyClickInput _clickInput = new();
        private readonly CompositeDisposable _subscriptions = new();
        private BlueprintCopyRangeVisualizer _visualizer;

        public override bool UsesPlacementHeight => true;

        public BlueprintCopySystem(Camera mainCamera, ClientBlueprintLibrary library, BlueprintNameInputState nameInputState, PlacementHeightOffset heightOffset, BlockGameObjectDataStore blockGameObjectDataStore)
        {
            _mainCamera = mainCamera;
            _library = library;
            _nameInputState = nameInputState;
            _heightOffset = heightOffset;
            _blockGameObjectDataStore = blockGameObjectDataStore;

            // 購読は一度だけ作り、Enableごとの重複を避ける
            // Subscribe once to avoid duplicates across Enable calls
            _nameInputState.OnConfirm.Subscribe(name => CreateAndReset(name).Forget()).AddTo(_subscriptions);
            _nameInputState.OnCancel.Subscribe(_ =>
            {
                if (_selection.Phase == BlueprintCopyPhase.AwaitingName) _selection.ReturnToEndSelection();
            }).AddTo(_subscriptions);
        }

        public override void Enable()
        {
            _visualizer ??= new BlueprintCopyRangeVisualizer();
            _selection.Clear();
            _clickInput.Reset();
        }

        protected override void ManualUpdate(BlueprintCopyPlacementTarget target, bool isSelectionChanged, PlacementFeedback feedback)
        {
            // 送信中は次の始点を受け付けない
            // Do not accept another start while creation is in flight
            if (_selection.Phase == BlueprintCopyPhase.Creating)
            {
                _visualizer.HideAll();
                return;
            }

            if (_selection.Phase == BlueprintCopyPhase.AwaitingName)
            {
                var (nameMin, nameMax) = BlueprintCopySelection.CalcBox(_selection.StartCell, _selection.EndCell);
                _visualizer.ShowAwaitingName(_selection.StartCell, _selection.EndCell, nameMin, nameMax);
                return;
            }

            PlacementHeightKeyInput.Apply(_heightOffset);
            var isClicked = _clickInput.TryConsumeClick();
            if (!PlacementUnitCellResolver.TryGetCursorCell(_mainCamera, _heightOffset.Value, out var cursorCell))
            {
                if (isClicked) Debug.Log("[BlueprintCopy] click refused: cursor has no placement surface");
                if (_selection.Phase == BlueprintCopyPhase.SelectingEnd) _visualizer.ShowStartOnly(_selection.StartCell);
                else _visualizer.HideAll();
                return;
            }

            if (_selection.Phase == BlueprintCopyPhase.SelectingStart)
            {
                _visualizer.ShowSelectingStart(cursorCell);
                if (isClicked) _selection.SelectStart(cursorCell);
                return;
            }

            var (min, max) = BlueprintCopySelection.CalcBox(_selection.StartCell, cursorCell);
            var count = BlueprintCopyRangeCounter.Count(EnumerateBlocks(), min, max);
            BlueprintCopyFeedbackLines.ReportBlocksInRange(count, feedback);
            _visualizer.ShowSelectingEnd(_selection.StartCell, cursorCell, min, max);

            if (!isClicked) return;
            if (count == 0)
            {
                // 空範囲は確定を拒み、ツールチップとログへ理由を残す
                // Refuse an empty range, leaving its reason in tooltip and log
                Debug.Log($"[BlueprintCopy] end click refused: no copy targets in {min}-{max}");
                return;
            }

            _selection.SelectEnd(cursorCell);
            _nameInputState.Open();

            #region Internal

            IEnumerable<(BlockMasterElement master, BlockPositionInfo position)> EnumerateBlocks()
            {
                return _blockGameObjectDataStore.BlockGameObjectByInstanceIdDictionary.Values.Select(block => (block.BlockMasterElement, block.BlockPosInfo));
            }

            #endregion
        }

        public override void Disable()
        {
            _selection.Clear();
            _clickInput.Reset();
            _visualizer?.HideAll();
            _nameInputState.Close();
        }

        public override bool TryCancelInProgressOperation()
        {
            switch (_selection.Phase)
            {
                case BlueprintCopyPhase.SelectingEnd:
                    _selection.Clear();
                    _visualizer?.HideAll();
                    return true;
                case BlueprintCopyPhase.AwaitingName:
                    _nameInputState.Close();
                    _selection.ReturnToEndSelection();
                    return true;
                default:
                    return false;
            }
        }

        private async UniTaskVoid CreateAndReset(string name)
        {
            var (min, max) = BlueprintCopySelection.CalcBox(_selection.StartCell, _selection.EndCell);
            _selection.BeginCreate();
            var result = await _library.CreateBlueprint(name, min, max, CancellationToken.None);

            // 応答まで送信中局面を保持し、完了後に選択を畳む
            // Hold Creating until the reply, then clear the selection
            if (_selection.Phase == BlueprintCopyPhase.Creating) _selection.Clear();
            if (result.Success) return;
            BlueprintCreateFailureNotifier.NotifyFailure(result, ClientDIContext.ClientLocalNotificationSource, min, max, name);
        }
    }
}
