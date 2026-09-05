using System.Collections.Generic;
using Client.Game.InGame.Interact;
using Client.Game.InGame.Interact.Selection;
using Client.Game.InGame.Interact.Tap;
using Client.Input;

namespace Client.Tests.Interact
{
    /// <summary>
    ///     選定結果だけを差し替えて、コントローラ側の振る舞いだけを見るための選定器
    ///     Selector that replaces only the selection outcome so tests observe just the controller's behaviour
    /// </summary>
    internal sealed class ScriptedInteractTargetSelector : IInteractTargetSelector, IInteractSelection
    {
        private readonly List<IInteractable> _candidates = new();

        private IInteractable _next;

        public IInteractable Primary => _next;

        public void SetNext(IInteractable next)
        {
            _next = next;
        }

        // 主対象の隣に居る別候補を足す
        // Adds another candidate standing beside the primary target
        public void AddCandidate(IInteractable candidate)
        {
            _candidates.Add(candidate);
        }

        // 台本どおりの結果をそのまま1フレーム分の走査結果として返す
        // Hands the scripted outcome back as the frame's scan result
        public IInteractSelection Scan()
        {
            return this;
        }

        // 主対象が応じるならそれを、応じないなら最初に応じる別候補を返す
        // Returns the primary target when it answers, otherwise the first candidate that does
        public IInteractable SelectRespondingTo(InputKey key)
        {
            if (RespondsTo(_next, key)) return _next;

            foreach (var candidate in _candidates)
                if (RespondsTo(candidate, key))
                    return candidate;

            return null;
        }

        public void CollectCandidateKeys(List<InputKey> keys)
        {
            keys.Clear();
            AddKeys(_next);
            foreach (var candidate in _candidates) AddKeys(candidate);

            #region Internal

            void AddKeys(IInteractable interactable)
            {
                if (interactable is not ITapInteractable tapInteractable) return;

                foreach (var action in tapInteractable.Actions)
                    if (!keys.Contains(action.Key))
                        keys.Add(action.Key);
            }

            #endregion
        }

        private static bool RespondsTo(IInteractable interactable, InputKey key)
        {
            if (interactable is not ITapInteractable tapInteractable) return false;

            foreach (var action in tapInteractable.Actions)
                if (action.Key == key)
                    return true;

            return false;
        }
    }
}
