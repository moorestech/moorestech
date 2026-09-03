using System.Collections.Generic;
using Client.Game.InGame.Interact;
using Client.Input;

namespace Client.Tests.Interact
{
    /// <summary>
    ///     選定結果だけを差し替えて、コントローラ側の振る舞いだけを見るための選定器
    ///     Selector that replaces only the selection outcome so tests observe just the controller's behaviour
    /// </summary>
    internal sealed class ScriptedInteractTargetSelector : IInteractTargetSelector
    {
        private IInteractable _next;

        public void SetNext(IInteractable next)
        {
            _next = next;
        }

        public IInteractable Select()
        {
            return _next;
        }

        // 候補は常に1件なので、応じるかどうかだけを見る
        // There is only ever one candidate, so only whether it answers the key matters
        public IInteractable SelectRespondingTo(InputKey key)
        {
            if (_next is not ITapInteractable tapInteractable) return null;

            foreach (var action in tapInteractable.Actions)
                if (action.Key == key)
                    return _next;

            return null;
        }

        public void CollectCandidateKeys(List<InputKey> keys)
        {
            keys.Clear();
            if (_next is not ITapInteractable tapInteractable) return;

            foreach (var action in tapInteractable.Actions)
                if (!keys.Contains(action.Key))
                    keys.Add(action.Key);
        }
    }
}
