using Client.Game.InGame.BlockSystem.PlaceSystem.Common.Run;
using Client.Input;
using Core.Master;
using UnityEngine;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.Common
{
    /// <summary>
    /// ドラッグ中のセッションと高さオフセットを保持
    /// Holds the running drag session and the height offset
    /// 終了時に高さは開始値へ戻す
    /// Ending a drag restores the starting height
    /// </summary>
    public class CommonBlockPlaceDragState
    {
        public int HeightOffset { get; private set; }

        // 左ドラッグ設置は押下から解放までが目に見える進行中操作になる
        // A left-drag placement is a visible in-progress operation from press to release
        public bool IsDragging => _session != null;

        private PlacementDragSession _session;
        private BlockId? _previousSelectedBlockId;

        public void ClearDrag()
        {
            _session = null;
        }

        public void UpdateHeightOffsetByInput()
        {
            if (HybridInput.GetKeyDown(KeyCode.Q)) //TODO InputManagerに移す
                AdjustHeightOffset(-1);
            else if (HybridInput.GetKeyDown(KeyCode.E)) AdjustHeightOffset(1);
        }

        // 高さオフセットを動かす唯一の入口。入力の解釈と値の保持を分ける
        // The only entry that moves the height offset, keeping input interpretation apart from the stored value
        public void AdjustHeightOffset(int delta)
        {
            HeightOffset += delta;
        }

        // 選択ブロック変更時に連続設置状態をリセット
        // Resets the drag session when the selected block changes
        public void SyncSelectedBlock(BlockId blockId)
        {
            if (_previousSelectedBlockId != blockId)
            {
                // 切替後の高さは常に地表基準に戻す
                // A block switch always returns the height to ground level
                _session = null;
                HeightOffset = 0;
            }
            _previousSelectedBlockId = blockId;
        }

        public void BeginDrag(Vector3Int startCell, PlacementHitSurfaceKind surfaceKind)
        {
            _session = new PlacementDragSession(startCell, surfaceKind, HeightOffset);
        }

        // ドラッグ中は押下時の面種別を使う。毎フレーム判定だと面と地面をまたいだ瞬間に列全体の挙動が往復する
        // A drag keeps the surface kind from its press; judging per frame makes the whole run flip as the cursor crosses between faces and ground
        public PlacementHitSurfaceKind ResolveSurfaceKind(PlacementHitSurfaceKind currentSurfaceKind)
        {
            return _session == null ? currentSurfaceKind : _session.SurfaceKind;
        }

        public Vector3Int ResolveDragStartCell(Vector3Int cursorCell)
        {
            return _session == null ? cursorCell : _session.StartCell;
        }

        // マウスアップで連続設置解除、高さを開始時へ戻す。戻り値は押下が登録されていたか
        // Clears the drag session on mouse-up and restores the starting height; returns whether a press was registered
        public bool EndDrag()
        {
            // 押下未登録の解放は無視する（ビルドメニュー選択クリックの解放が漏れても高さを書き換えない）
            // Ignore releases without a registered press, so a leaked build-menu click release never rewrites the height
            if (_session == null) return false;

            HeightOffset = _session.StartHeightOffset;
            _session = null;
            return true;
        }
    }
}
