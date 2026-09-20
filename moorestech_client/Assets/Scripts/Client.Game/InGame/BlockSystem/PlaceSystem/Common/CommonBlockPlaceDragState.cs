using Client.Game.InGame.BlockSystem.PlaceSystem.Common.Run;
using Client.Input;
using Core.Master;
using UnityEngine;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.Common
{
    /// <summary>
    /// ドラッグセッションを保持。高さは共有PlacementHeightOffsetを読み書きするだけで保持しない
    /// 終了時に高さは開始値へ戻す
    /// Holds the drag session; the height offset is only read/written via the shared PlacementHeightOffset, not held here
    /// Ending a drag restores the starting height
    /// </summary>
    public class CommonBlockPlaceDragState
    {
        public int HeightOffset => _heightOffset.Value;

        // 左ドラッグ設置は押下から解放までが目に見える進行中操作になる
        // A left-drag placement is a visible in-progress operation from press to release
        public bool IsDragging => _session != null;

        private readonly PlacementHeightOffset _heightOffset;
        private PlacementDragSession _session;

        public CommonBlockPlaceDragState(PlacementHeightOffset heightOffset)
        {
            _heightOffset = heightOffset;
        }

        // 進行中ドラッグを畳む。ドラッグ中に上げた高さは一時的なものなので開始値へ戻す
        // Folds an in-progress drag; the height raised during it is temporary, so it returns to the starting value
        public void ClearDrag()
        {
            if (_session == null) return;

            _heightOffset.Restore(_session.StartHeightOffset);
            _session = null;
        }

        public void UpdateHeightOffsetByInput()
        {
            if (HybridInput.GetKeyDown(KeyCode.Q)) //TODO InputManagerに移す
                AdjustHeightOffset(-1);
            else if (HybridInput.GetKeyDown(KeyCode.E)) AdjustHeightOffset(1);
        }

        // 入力の解釈だけを担い、高さの規則と保持は共有の正へ委ねる
        // Interprets input only; the height rule and the stored value belong to the shared source
        public void AdjustHeightOffset(int delta)
        {
            _heightOffset.Adjust(delta);
        }

        // 持ち替え判定は共有の正が持つ。こちらは自分のドラッグを畳むだけ
        // The shared source owns the block-switch check; this only folds its own drag
        public void SyncSelectedBlock(BlockId blockId)
        {
            if (_heightOffset.SyncSelectedBlock(blockId)) _session = null;
        }

        public void BeginDrag(Vector3Int startCell, PlacementHitSurfaceKind surfaceKind)
        {
            _session = new PlacementDragSession(startCell, surfaceKind, HeightOffset);
        }

        // 起点復帰で軸未決化、離脱初回で長軸を先行に。軸未決のうちはZ先行を既定にする
        // Returning to start clears the axis; the first departure leads with the longer axis, defaulting to Z while undecided
        public bool ResolveDragAxisIsZ(Vector3Int dragStartCell, Vector3Int cursorCell)
        {
            // ドラッグ外は軸を持ち越さない。起点＝カーソルなので既定のZ先行と一致する
            // Outside a drag there is no axis to carry, and start equals cursor, so the Z-leading default applies
            if (_session == null) return true;

            if (dragStartCell == cursorCell) _session.SetDragAxisIsZ(null);
            else if (!_session.DragAxisIsZ.HasValue) _session.SetDragAxisIsZ(Mathf.Abs(cursorCell.x - dragStartCell.x) < Mathf.Abs(cursorCell.z - dragStartCell.z));

            return _session.DragAxisIsZ ?? true;
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

            ClearDrag();
            return true;
        }
    }
}
