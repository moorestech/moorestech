using Client.Game.InGame.Control;
using Client.Game.InGame.UI.Tooltip;
using Client.Input;
using UnityEngine.EventSystems;

namespace Client.Game.InGame.UI.UIState.State.DragDelete
{
    // 破壊モードの削除インタラクション（ホバー・ドラッグ選択・一括削除・ESCキャンセル）を担うサービス
    // Service owning destroy-mode delete interaction (hover, drag selection, bulk delete, ESC cancel)
    public class DeleteObjectService
    {
        private readonly DragDeleteSelection _selection = new();
        private IDeleteTarget _deleteTargetObject;
        private bool _isRemoveDeniedReasonShown;
        private bool _isDragging;

        public void Update()
        {
            // 拒否理由ツールチップを毎フレーム先に消す
            // Reset the denial-reason tooltip at the start of each frame
            if (_isRemoveDeniedReasonShown)
            {
                MouseCursorTooltip.Instance.Hide();
                _isRemoveDeniedReasonShown = false;
            }

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

            #region Internal

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

        // モード離脱時に進行中のプレビューと選択状態を片付ける
        // Clean up in-progress previews and selection state when leaving the mode
        public void Cleanup()
        {
            if (_deleteTargetObject != null)
            {
                _deleteTargetObject.ResetMaterial();
                _deleteTargetObject = null;
            }

            MouseCursorTooltip.Instance.Hide();
            _isRemoveDeniedReasonShown = false;
            _selection.CancelSelection();
            _isDragging = false;
        }
    }
}
