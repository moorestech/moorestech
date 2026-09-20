using System;
using System.Collections.Generic;
using Client.Game.InGame.Interact;
using Client.Game.InGame.Interact.Tap;
using Client.Game.InGame.UI.UIState;
using Client.Input;
using Mooresmaster.Localization.Generated;

namespace Client.Tests.Interact
{
    // 実行回数を数える単押しアクション
    // A tap action that counts its executions
    internal sealed class StubTapInteractAction : ITapInteractAction
    {
        private readonly UIStateEnum _nextState;

        public InputKey Key { get; }
        public LocalizationKey HintKey { get; }
        public IReadOnlyList<string> HintParams => Array.Empty<string>();
        public int ExecutedCount { get; private set; }

        public StubTapInteractAction(InputKey key, LocalizationKey hintKey, UIStateEnum nextState)
        {
            Key = key;
            HintKey = hintKey;
            _nextState = nextState;
        }

        public InteractExecuteResult Execute()
        {
            ExecutedCount++;
            return InteractExecuteResult.Transit(new UITransitContext(_nextState));
        }
    }
}
