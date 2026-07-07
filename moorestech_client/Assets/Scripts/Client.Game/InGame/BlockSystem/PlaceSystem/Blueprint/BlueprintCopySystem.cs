using System.Threading;
using Client.Game.InGame.BlockSystem.PlaceSystem.Util;
using Client.Game.InGame.UI.Blueprint;
using Client.Input;
using Cysharp.Threading.Tasks;
using UniRx;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.Blueprint
{
    /// <summary>
    ///     XZドラッグ＋スクロール高さ調整でBP登録範囲を選択し、名前入力後にCreateを送る
    ///     Selects the blueprint box via XZ drag plus scroll height, then sends Create after name input
    /// </summary>
    public class BlueprintCopySystem : IPlaceSystem
    {
        private readonly ClientBlueprintLibrary _library;
        private readonly BlueprintNameInputView _nameInputView;
        private readonly Camera _mainCamera;
        private BlueprintAreaVisualizer _visualizer;

        private bool _isDragging;
        private Vector3Int _dragStart;
        private Vector3Int _dragEnd;
        private int _topYOffset;
        private float _scrollAccumulator;
        private bool _isAwaitingName;

        // ドラッグ開始時の上面高さの初期値（スクロールで調整可能）
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
        }

        public void Enable()
        {
            _visualizer ??= new BlueprintAreaVisualizer();
        }

        public void ManualUpdate(PlaceSystemUpdateContext context)
        {
            // 名前入力ダイアログ表示中はドラッグ操作を止める
            // Freeze drag interaction while the name dialog is open
            if (_isAwaitingName) return;

            HandleDragStart();
            UpdateDrag();
            HandleRelease();
            HandleCancel();

            #region Internal

            void HandleDragStart()
            {
                if (!InputManager.Playable.ScreenLeftClick.GetKeyDown) return;
                if (EventSystem.current.IsPointerOverGameObject()) return;
                if (!PlaceSystemUtil.TryGetRayHitPosition(_mainCamera, out var hit, out _)) return;

                _dragStart = SnapToCell(hit);
                _dragEnd = _dragStart;
                _topYOffset = DefaultTopYOffset;
                _scrollAccumulator = 0f;
                _isDragging = true;
            }

            void UpdateDrag()
            {
                if (!_isDragging || !InputManager.Playable.ScreenLeftClick.GetKey) return;
                if (PlaceSystemUtil.TryGetRayHitPosition(_mainCamera, out var hit, out _)) _dragEnd = SnapToCell(hit);

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

            void HandleCancel()
            {
                // 現状はPlaceBlockStateがESCを先に消費するため未到達（他状態から駆動された場合の保険として残置）
                // Currently unreachable because PlaceBlockState consumes ESC first; kept as insurance when driven from other states
                if (!InputManager.UI.CloseUI.GetKeyDown) return;
                ResetSelection();
            }

            #endregion
        }

        public void Disable()
        {
            ResetSelection();
            _nameInputView.Close();
            _isAwaitingName = false;
        }

        private static float ReadScrollDelta()
        {
            // ホットバーと同じスケールでInputSystemスクロールを読む（入力注入対応。無ければlegacyへフォールバック）
            // Read Input System scroll at the hot bar's scale (supports input injection); fall back to legacy Input
            return Mouse.current != null ? Mouse.current.scroll.ReadValue().y / 100f : UnityEngine.Input.mouseScrollDelta.y;
        }

        private static Vector3Int SnapToCell(Vector3 hitPoint)
        {
            // 貼り付けアンカーと同じ規約でセル化する（XZは床スナップ、Yは整数グリッド面の丸め）
            // Snap to a cell with the paste-anchor convention: floor XZ, round Y on the integer grid face
            return new Vector3Int(Mathf.FloorToInt(hitPoint.x), Mathf.RoundToInt(hitPoint.y), Mathf.FloorToInt(hitPoint.z));
        }

        private (Vector3Int min, Vector3Int max) CalcBox()
        {
            // XZはドラッグ両端、Yはドラッグ面からスクロール調整分までのボックス
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

        private void SubscribeNameInput()
        {
            // 確定でCreate送信（アンカーはサーバーがボックスから導出）、キャンセルで選択解除
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
    }
}
