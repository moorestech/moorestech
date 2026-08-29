using System.Threading;
using Client.Game.InGame.BlockSystem.PlaceSystem.Feedback;
using Client.Game.InGame.BlockSystem.PlaceSystem.Util;
using Client.Game.InGame.BlockSystem.PlaceSystem.Targets;
using Client.Game.InGame.Control;
using Client.Game.InGame.UI.Blueprint;
using Client.Input;
using Cysharp.Threading.Tasks;
using UniRx;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.Blueprint
{
    /// <summary>
    ///     ・XZドラッグ+スクロールで範囲選択
    ///     ・名前入力後にCreate送信
    ///     Selects the blueprint box via XZ drag plus scroll height, then sends Create after name input
    /// </summary>
    public class BlueprintCopySystem : PlaceSystemBase<BlueprintCopyPlacementTarget>
    {
        private readonly ClientBlueprintLibrary _library;
        private readonly BlueprintNameInputView _nameInputView;
        private readonly Camera _mainCamera;
        private BlueprintAreaVisualizer _visualizer;

        private bool _isDragging;

        // ホイールで高さを変えるのはドラッグ中だけ。非ドラッグ時は装備切替へ譲る
        // The wheel changes height only while dragging; outside a drag it yields to equipment switching
        public override bool OwnsWheelInput => _isDragging;
        private Vector3Int _dragStart;
        private Vector3Int _dragEnd;
        private int _topYOffset;
        private float _scrollAccumulator;
        private bool _isAwaitingName;

        // 開始時の上面高さ初期値（スクロール調整可）
        // Initial box height above the drag plane; adjustable via scroll
        private const int DefaultTopYOffset = 4;

        public BlueprintCopySystem(Camera mainCamera, ClientBlueprintLibrary library, BlueprintNameInputView nameInputView)
        {
            _mainCamera = mainCamera;
            _library = library;
            _nameInputView = nameInputView;

            // Enable毎の重複購読を避けるため購読はコンストラクタで1回だけ行う
            // Subscribe once in the constructor to avoid duplicate subscriptions on repeated Enable
            SubscribeNameInput();

            #region Internal

            void SubscribeNameInput()
            {
                // 確定でCreate送信、キャンセルで選択解除
                // Confirm sends Create (the server derives the anchor from the box); cancel clears the selection
                _nameInputView.OnConfirm.Subscribe(name =>
                {
                    var (min, max) = CalcBox();
                    _library.CreateBlueprint(name, min, max, CancellationToken.None).Forget();
                    _isAwaitingName = false;
                    ResetSelection();
                }).AddTo(_nameInputView);

                _nameInputView.OnCancel.Subscribe(_ =>
                {
                    _isAwaitingName = false;
                    ResetSelection();
                }).AddTo(_nameInputView);
            }

            #endregion
        }

        public override void Enable()
        {
            _visualizer ??= new BlueprintAreaVisualizer();
        }

        protected override void ManualUpdate(BlueprintCopyPlacementTarget target, bool isSelectionChanged, PlacementFeedback feedback)
        {
            // 名前入力中はドラッグを停止
            // Freeze drag interaction while the name dialog is open
            if (_isAwaitingName) return;

            HandleDragStart();
            UpdateDrag();
            HandleRelease();

            #region Internal

            void HandleDragStart()
            {
                if (!InputManager.Playable.ScreenLeftClick.GetKeyDown) return;
                if (UiPointerHitTest.IsPointerOverAnyUi()) return;
                if (!PlaceSystemUtil.TryGetRayHitPosition(_mainCamera, out var hit, out _)) return;

                _dragStart = PlaceSystemUtil.SnapHitPointToCell(hit);
                _dragEnd = _dragStart;
                _topYOffset = DefaultTopYOffset;
                _scrollAccumulator = 0f;
                _isDragging = true;
            }

            void UpdateDrag()
            {
                if (!_isDragging || !InputManager.Playable.ScreenLeftClick.GetKey) return;
                if (PlaceSystemUtil.TryGetRayHitPosition(_mainCamera, out var hit, out _)) _dragEnd = PlaceSystemUtil.SnapHitPointToCell(hit);

                // スクロールで上面高さを1セル単位で調整（下限0=1段選択。微小デルタは蓄積して整数化）
                // Scroll adjusts the box top per cell, floored at 0; fractional deltas accumulate into whole steps
                _scrollAccumulator += ReadScrollDelta();
                var scrollStep = (int)_scrollAccumulator;
                if (scrollStep != 0)
                {
                    _scrollAccumulator -= scrollStep;
                    _topYOffset = Mathf.Max(0, _topYOffset + scrollStep);
                }

                var (min, max) = CalcBox();
                _visualizer.Show(min, max);
            }

            void HandleRelease()
            {
                if (!_isDragging || !InputManager.Playable.ScreenLeftClick.GetKeyUp) return;

                _isDragging = false;
                _isAwaitingName = true;
                _nameInputView.Open();
            }

            float ReadScrollDelta()
            {
                if (UiPointerHitTest.IsPointerOverAnyUi()) return 0f;

                // ホットバーと同じスケールでInputSystemスクロールを読む（入力注入対応。無ければlegacyへフォールバック）
                // Read Input System scroll at the hot bar's scale (supports input injection); fall back to legacy Input
                return Mouse.current != null ? Mouse.current.scroll.ReadValue().y / 100f : UnityEngine.Input.mouseScrollDelta.y;
            }

            #endregion
        }

        public override void Disable()
        {
            ResetSelection();
            _nameInputView.Close();
            _isAwaitingName = false;
        }

        // 右短押し/Escで範囲選択中のドラッグだけを解除する。ドラッグしていなければ解除対象なし
        // A right short press / Esc cancels only an in-progress box drag; without a drag there is nothing to cancel
        public override bool TryCancelInProgressOperation()
        {
            if (!_isDragging) return false;

            ResetSelection();
            return true;
        }

        private (Vector3Int min, Vector3Int max) CalcBox()
        {
            // XZは両端・Yはスクロール分のボックス
            // XZ from drag endpoints; Y spans the drag plane up to the scroll offset
            var min = new Vector3Int(
                Mathf.Min(_dragStart.x, _dragEnd.x),
                Mathf.Min(_dragStart.y, _dragEnd.y),
                Mathf.Min(_dragStart.z, _dragEnd.z));
            var max = new Vector3Int(
                Mathf.Max(_dragStart.x, _dragEnd.x),
                Mathf.Max(_dragStart.y, _dragEnd.y) + _topYOffset,
                Mathf.Max(_dragStart.z, _dragEnd.z));
            return (min, max);
        }

        private void ResetSelection()
        {
            _isDragging = false;
            _visualizer?.Hide();
        }
    }
}
