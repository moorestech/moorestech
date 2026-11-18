using System;
using System.Collections.Generic;

namespace Client.Ui.State.Tutorial.KeyControlHint
{
    public sealed class KeyControlHintViewModel
    {
        public static readonly KeyControlHintViewModel Empty = new(Array.Empty<KeyControlHintCue>());

        public readonly IReadOnlyList<KeyControlHintCue> Hints;

        public KeyControlHintViewModel(IReadOnlyList<KeyControlHintCue> hints)
        {
            Hints = hints;
        }
    }
}
