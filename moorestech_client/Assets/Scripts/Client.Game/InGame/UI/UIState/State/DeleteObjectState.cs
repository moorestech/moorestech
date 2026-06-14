using System;
using Client.Game.InGame.Block;
using Client.Game.InGame.Control;
using Client.Game.InGame.Entity.Object;
using Client.Game.InGame.Train.RailGraph;
using Client.Game.InGame.UI.KeyControl;
using Client.Game.InGame.UI.Tooltip;
using Client.Game.InGame.UI.UIState.Input;
using Client.Game.InGame.UI.UIState.State.DragDelete;
using Client.Game.InGame.UI.UIState.UIObject;
using Client.Input;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Client.Game.InGame.UI.UIState.State
{
    public class DeleteObjectState : IUIState
    {
        private readonly DeleteBarObject _deleteBarObject;

        private readonly ScreenClickableCameraController _screenClickableCameraController;

        private IDeleteTarget _deleteTargetObject;
        private bool _isRemoveDeniedReasonShown;

        private readonly DragDeleteSelection _selection = new();
        private bool _isDragging;

        private readonly RailGraphClientCache _railGraphClientCache;

        public DeleteObjectState(DeleteBarObject deleteBarObject, InGameCameraController inGameCameraController, RailGraphClientCache cache)
        {
            _screenClickableCameraController = new ScreenClickableCameraController(inGameCameraController);
            _deleteBarObject = deleteBarObject;
            _railGraphClientCache = cache;
            deleteBarObject.gameObject.SetActive(false);
        }

        public void OnEnter(UITransitContext context)
        {
            _screenClickableCameraController.OnEnter(false);
            _deleteBarObject.gameObject.SetActive(true);
            KeyControlDescription.Instance.SetText("ドラッグ: まとめて選択\n離す: まとめて削除\nESC: 選択キャンセル\nG: 破壊モード終了\nB: 設置モード\nTab: インベントリ");
        }

        public UITransitContext GetNextUpdate()
        {
            // 拒否理由ツールチップを毎フレーム先に消す
            // Reset the denial-reason tooltip at the start of each frame
            if (_isRemoveDeniedReasonShown)
            {
                MouseCursorTooltip.Instance.Hide();
                _isRemoveDeniedReasonShown = false;
            }

            // モード遷移を判定する（ESCはモードを抜けない）
            // Handle mode transitions (ESC no longer exits the mode)
            var transit = HandleTransition();
            if (transit != null) return transit;

            // ESCは選択キャンセルとして扱いモードに留まる
            // Treat ESC as a selection cancel while staying in this mode
            if (InputManager.UI.CloseUI.GetKeyDown) CancelOnEscape();

            // カーソル下の削除対象を取得（無ければnull）
            // Resolve the target hovered this frame (null when nothing hit)
            BlockClickDetectUtil.TryGetCursorOnComponent(out IDeleteTarget hovered);

            // 左クリック開始でドラッグ選択を開始する
            // Begin a drag selection on left-click down
            HandleDragStart();

            // ドラッグ中は選択へ追加、非ドラッグは単体表示
            // While dragging accumulate the selection, otherwise show single hover preview
            if (_isDragging) UpdateDragSelection();
            else UpdateSingleHoverPreview();

            // 左クリック離しで選択を確定して削除する
            // Commit and delete the selection on left-click release
            HandleRelease();

            _screenClickableCameraController.GetNextUpdate();
            return null;

            #region Internal

            UITransitContext HandleTransition()
            {
                // ESCはOpenMenuとCloseUI両方にbindされ、OpenMenuを先に拾うと選択キャンセルが死ぬため破壊モードでは扱わない
                // ESC is bound to both OpenMenu and CloseUI; handling OpenMenu here would shadow the cancel path, so skip it in destroy mode
                if (InputManager.UI.BlockDelete.GetKeyDown) return new UITransitContext(UIStateEnum.GameScreen);
                if (UnityEngine.Input.GetKeyDown(KeyCode.B)) return new UITransitContext(UIStateEnum.PlaceBlock);
                if (InputManager.UI.OpenInventory.GetKeyDown) return new UITransitContext(UIStateEnum.PlayerInventory);
                return null;
            }

            void CancelOnEscape()
            {
                _selection.CancelSelection();

                // 単体ホバー中の赤プレビューも確実に戻す
                // Also clear any single-hover red preview to be safe
                if (!_isDragging && _deleteTargetObject != null)
                {
                    _deleteTargetObject.ResetMaterial();
                    _deleteTargetObject = null;
                }
            }

            void HandleDragStart()
            {
                if (!InputManager.Playable.ScreenLeftClick.GetKeyDown) return;
                if (EventSystem.current.IsPointerOverGameObject()) return;

                // 単体プレビューの所有権を選択側へ渡す
                // Hand off preview ownership from single-hover to the selection
                if (_deleteTargetObject != null)
                {
                    _deleteTargetObject.ResetMaterial();
                    _deleteTargetObject = null;
                }

                _selection.BeginDrag();
                _isDragging = true;
            }

            void UpdateDragSelection()
            {
                // キャンセル済みドラッグは何もしない（ESC後は離すまで不活性）
                // A canceled drag is inert until the button is released
                if (!_selection.CanCommit()) return;

                if (hovered == null) return;

                if (hovered.IsRemovable(out var reason))
                {
                    _selection.AddTarget(hovered);
                }
                else
                {
                    MouseCursorTooltip.Instance.Show(reason, isLocalize: false);
                    _isRemoveDeniedReasonShown = true;
                }
            }

            void UpdateSingleHoverPreview()
            {
                // ホバー対象が変われば旧を戻し新を表示する
                // Swap preview when the hovered target changes
                if (hovered != null)
                {
                    if (_deleteTargetObject == null || _deleteTargetObject != hovered)
                    {
                        if (_deleteTargetObject != null) _deleteTargetObject.ResetMaterial();
                        _deleteTargetObject = hovered;
                        _deleteTargetObject.SetRemovePreviewing();
                    }
                }
                else if (_deleteTargetObject != null)
                {
                    _deleteTargetObject.ResetMaterial();
                    _deleteTargetObject = null;
                }

                // 削除不可な対象は理由ツールチップだけ表示する
                // For a non-removable target only show the denial tooltip
                if (_deleteTargetObject != null && !_deleteTargetObject.IsRemovable(out var reason))
                {
                    MouseCursorTooltip.Instance.Show(reason, isLocalize: false);
                    _isRemoveDeniedReasonShown = true;
                }
            }

            void HandleRelease()
            {
                if (!InputManager.Playable.ScreenLeftClick.GetKeyUp) return;

                if (_isDragging && _selection.CanCommit()) _selection.CommitDelete();
                _isDragging = false;
            }

            #endregion
        }


        public void OnExit()
        {
            if (_deleteTargetObject != null) _deleteTargetObject.ResetMaterial();
            _deleteBarObject.gameObject.SetActive(false);

            // モード離脱時に進行中の選択プレビューを片付ける
            // Clear any in-progress selection previews when leaving the mode
            _selection.CancelSelection();
            _isDragging = false;

            _screenClickableCameraController.OnExit();
        }
    }
}
