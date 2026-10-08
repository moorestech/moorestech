using Client.Game.InGame.BlockSystem.PlaceSystem.Common;
using Client.Game.InGame.BlockSystem.PlaceSystem.Common.Run;
using UnityEngine;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.Blueprint.Paste
{
    /// <summary>
    ///     貼り付けドラッグの始点と開始高さを保持する
    ///     Holds the paste drag's start anchor and starting height
    /// </summary>
    public class BlueprintPasteDragState
    {
        public bool IsDragging => _session != null;
        private readonly PlacementHeightOffset _heightOffset;
        private PlacementDragSession _session;

        public BlueprintPasteDragState(PlacementHeightOffset heightOffset)
        {
            _heightOffset = heightOffset;
        }

        public void BeginDrag(Vector3Int startAnchor, int startHeightOffset)
        {
            _session = new PlacementDragSession(startAnchor, PlacementHitSurfaceKind.Ground, startHeightOffset);
        }

        public Vector3Int ResolveStartAnchor(Vector3Int cursorAnchor)
        {
            return _session == null ? cursorAnchor : _session.StartCell;
        }

        internal Vector3Int? GetStartAnchor()
        {
            return _session?.StartCell;
        }

        public bool EndDrag()
        {
            if (_session == null) return false;
            ClearDrag();
            return true;
        }

        public void ClearDrag()
        {
            if (_session == null) return;
            _heightOffset.Restore(_session.StartHeightOffset);
            _session = null;
        }

        // 持ち替えではコントローラの高さ0復帰を上書きしない
        // Do not overwrite the controller's height reset on target changes
        public void DiscardForSelectionChange()
        {
            _session = null;
        }
    }
}
