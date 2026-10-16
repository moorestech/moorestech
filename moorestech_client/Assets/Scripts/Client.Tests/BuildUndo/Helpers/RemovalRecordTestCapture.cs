using System.Collections.Generic;
using Client.Game.InGame.BlockSystem.PlaceSystem.Undo;
using Client.Game.InGame.BlockSystem.PlaceSystem.Undo.Removal;
using Client.Game.InGame.UI.UIState.State;

namespace Client.Tests.BuildUndo
{
    internal static class RemovalRecordTestCapture
    {
        internal static RemoveOperationRecord CreateFrom(IReadOnlyList<IDeleteTarget> targets, IRemovalRestoreSender sender)
        {
            var captures = new List<RemovedObjectCollector>();
            foreach (var target in targets)
            {
                var capture = new RemovedObjectCollector();
                target.CollectRemovedObjects(capture);
                captures.Add(capture);
            }
            return RemoveOperationRecord.CreateFromCollectors(captures, sender);
        }
    }
}
