using Client.Game.InGame.Interact;

namespace Client.Tests.Interact
{
    /// <summary>
    ///     選定結果だけを差し替えて、コントローラ側の振る舞いだけを見るための選定器
    ///     Selector that replaces only the selection outcome so tests observe just the controller's behaviour
    /// </summary>
    internal sealed class ScriptedInteractTargetSelector : InteractTargetSelector
    {
        private IInteractable _next;

        public void SetNext(IInteractable next)
        {
            _next = next;
        }

        public override IInteractable Select()
        {
            return _next;
        }
    }
}
