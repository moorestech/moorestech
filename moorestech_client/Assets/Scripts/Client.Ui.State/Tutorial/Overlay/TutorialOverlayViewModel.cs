using System;
using System.Collections.Generic;

namespace Client.Ui.State.Tutorial.Overlay
{
    public sealed class TutorialOverlayViewModel
    {
        public static readonly TutorialOverlayViewModel Empty = new(Array.Empty<TutorialOutlineCue>(), Array.Empty<TutorialDragGuideCue>());

        public readonly IReadOnlyList<TutorialOutlineCue> Outlines;
        public readonly IReadOnlyList<TutorialDragGuideCue> DragGuides;

        public TutorialOverlayViewModel(IReadOnlyList<TutorialOutlineCue> outlines, IReadOnlyList<TutorialDragGuideCue> dragGuides)
        {
            Outlines = outlines;
            DragGuides = dragGuides;
        }
    }
}
