using System;
using System.Collections.Generic;

namespace Client.Ui.State.Tutorial.WorldPin
{
    public sealed class WorldPinViewModel
    {
        public static readonly WorldPinViewModel Empty = new(Array.Empty<WorldPinCue>());

        public readonly IReadOnlyList<WorldPinCue> Pins;

        // 生成は WorldPinViewModelProjection だけ（blocking スキット抑制と GUID 検証を迂回させない）
        // Only WorldPinViewModelProjection creates it (so the blocking-skit suppression and GUID check cannot be bypassed)
        internal WorldPinViewModel(IReadOnlyList<WorldPinCue> pins)
        {
            Pins = pins;
        }
    }
}
