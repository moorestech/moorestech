using Client.Game.InGame.BlockSystem.PlaceSystem.Blueprint.Paste;
using Client.Game.InGame.BlockSystem.PlaceSystem.Common;
using NUnit.Framework;
using UnityEngine;

namespace Client.Tests.PlaceSystem
{
    public class BlueprintPasteDragStateTest
    {
        [Test]
        public void 解放は開始高さへ戻し次の始点を消す()
        {
            var height = new PlacementHeightOffset();
            var drag = new BlueprintPasteDragState(height);
            drag.BeginDrag(new Vector3Int(6, 32, 6), 0);
            height.Adjust(2);

            Assert.IsTrue(drag.EndDrag());
            Assert.AreEqual(0, height.Value);
            Assert.IsFalse(drag.IsDragging);
            Assert.IsFalse(drag.EndDrag());
        }

        [Test]
        public void 持ち替えではコントローラの高さ復帰を上書きしない()
        {
            var height = new PlacementHeightOffset();
            var drag = new BlueprintPasteDragState(height);
            height.Adjust(2);
            drag.BeginDrag(Vector3Int.zero, 2);
            height.ResetToGround();

            drag.DiscardForSelectionChange();

            Assert.AreEqual(0, height.Value);
            Assert.IsFalse(drag.IsDragging);
        }
    }
}
