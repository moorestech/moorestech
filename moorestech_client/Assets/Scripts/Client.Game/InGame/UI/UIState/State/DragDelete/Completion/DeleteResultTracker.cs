using System.Collections.Generic;
using Mooresmaster.Localization.Generated;

namespace Client.Game.InGame.UI.UIState.State.DragDelete.Completion
{
    internal sealed class DeleteResultTracker
    {
        private readonly HashSet<object> _pending = new();
        internal bool IsPending(object key) => _pending.Contains(key);
        internal void Receive(DeleteCompletion result)
        {
            if (result.Kind == DeleteCompletionKind.Pending || result.Kind == DeleteCompletionKind.Unconfirmed) _pending.Add(result.TargetKey);
            else _pending.Remove(result.TargetKey);
        }
    }
}
