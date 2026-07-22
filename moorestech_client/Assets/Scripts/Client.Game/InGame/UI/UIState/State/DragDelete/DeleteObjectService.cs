using Client.Game.InGame.Block;
using Client.Game.InGame.BlockSystem.PlaceSystem.Undo;
using Client.Game.InGame.Control;
using Client.Game.InGame.UI.Tooltip;
using Client.Input;
using System;
using System.Collections.Generic;
using UniRx;

namespace Client.Game.InGame.UI.UIState.State.DragDelete
{
    // 破壊モードの削除インタラクション（ホバー・ドラッグ選択・一括削除・ESCキャンセル）を担うサービス
    // Service owning destroy-mode delete interaction (hover, drag selection, bulk delete, ESC cancel)
    public class DeleteObjectService
    {
        private readonly DragDeleteSelection _selection = new();
        private readonly BuildOperationHistory _buildOperationHistory;
        private IDeleteTarget _deleteTargetObject;
        private bool _isRemoveDeniedReasonShown;
        private bool _isDragging;
        private readonly ReactiveProperty<string> _unavailableReason = new("");

        public IObservable<string> OnUnavailableReasonChanged => _unavailableReason;
        public string GetUnavailableReason() => _unavailableReason.Value;

        public DeleteObjectService(BuildOperationHistory buildOperationHistory)
        {
            _buildOperationHistory = buildOperationHistory;
        }

        public void Update()
        {
            // 拒否理由ツールチップを毎フレーム先に消す
            // Reset the denial-reason tooltip at the start of each frame
            if (_isRemoveDeniedReasonShown)
            {
                MouseCursorTooltip.Instance.Hide();
                _isRemoveDeniedReasonShown = false;
                _unavailableReason.Value = "";
            }

            // カーソル下の削除対象を取得（無ければnull）
            // Resolve the target hovered this frame (null when nothing hit)
            BlockClickDetectUtil.TryGetCursorOnComponent(out IDeleteTarget hovered);

            // 左クリック開始でドラッグ選択を開始する
            // Begin a drag selection on left-click down
            HandleDragStart();

            // ドラッグ中は選択へ追加、ボタン非押下時のみ単体ホバー表示（ESCキャンセル後の押下中は何も出さない）
            // Dragging accumulates the selection; single hover shows only while the button is up (nothing during a dead post-ESC hold)
            if (_isDragging) UpdateDragSelection();
            else if (!InputManager.Playable.ScreenLeftClick.GetKey) UpdateSingleHoverPreview();

            // 左クリック離しで選択を確定して削除する
            // Commit and delete the selection on left-click release
            HandleRelease();

            #region Internal

            void HandleDragStart()
            {
                if (!InputManager.Playable.ScreenLeftClick.GetKeyDown) return;
                if (UiPointerHitTest.IsPointerOverAnyUi()) return;

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

                // 削除可否・カテゴリー整合の判定と追加をサービス側へ集約し、拒否理由だけ受け取って表示する
                // Delegate the removable/category judgement and the add to the service; just receive and show the deny reason
                if (!_selection.TryAddTarget(hovered, out var denyReason))
                {
                    MouseCursorTooltip.Instance.Show(denyReason, isLocalize: false);
                    _unavailableReason.Value = denyReason;
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
                    _unavailableReason.Value = reason;
                    _isRemoveDeniedReasonShown = true;
                }
            }

            void HandleRelease()
            {
                if (!InputManager.Playable.ScreenLeftClick.GetKeyUp) return;

                if (_isDragging && _selection.CanCommit()) RecordAndCommitDelete();
                _isDragging = false;
            }

            void RecordAndCommitDelete()
            {
                var committed = _selection.CommitDelete();

                // ブロック対象だけをCtrl+Z用に楽観的記録（撤去失敗セルはUndo時の空き座標ガードで自然に無効化）
                // Optimistically record block targets for Ctrl+Z (failed removals are neutralized by the empty-cell guard on undo)
                var removedBlocks = new List<RemovedBlockInfo>();
                foreach (var target in committed)
                {
                    if (target is not BlockGameObjectChild blockChild) continue;
                    var blockGameObject = blockChild.BlockGameObject;
                    removedBlocks.Add(new RemovedBlockInfo(
                        blockGameObject.BlockPosInfo.OriginalPos,
                        blockGameObject.BlockId,
                        blockGameObject.BlockPosInfo.BlockDirection));
                }
                if (removedBlocks.Count != 0) _buildOperationHistory.Push(new RemoveOperationRecord(removedBlocks));
            }

            #endregion
        }

        // 選択があればキャンセルしtrueを返す。選択が無ければfalseを返し、呼び出し側がモード終了を判断する
        // Cancel the selection and return true if one exists; return false (nothing to cancel) so the caller can decide to exit the mode
        public bool TryCancelSelection()
        {
            if (!_selection.HasSelection()) return false;

            CancelSelection();
            return true;
        }

        // 選択とプレビュー・ツールチップ・ドラッグ状態を全て片付ける（ESCの選択キャンセルとモード離脱の両方で使う）
        // Clear the selection, previews, tooltip, and drag state (used by both ESC cancel and mode exit)
        public void CancelSelection()
        {
            _selection.CancelSelection();

            // ホバー中の赤プレビューを戻す
            // Reset any single-hover red preview
            if (_deleteTargetObject != null)
            {
                _deleteTargetObject.ResetMaterial();
                _deleteTargetObject = null;
            }

            MouseCursorTooltip.Instance.Hide();
            _isRemoveDeniedReasonShown = false;
            _isDragging = false;
            _unavailableReason.Value = "";
        }
    }
}
