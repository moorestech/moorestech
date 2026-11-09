using System;
using System.Collections.Generic;

namespace Client.Ui.State.Tutorial.Overlay
{
    public sealed class TutorialOverlayViewModel
    {
        public static readonly TutorialOverlayViewModel Empty = new(0, Array.Empty<TutorialOutlineCue>(), Array.Empty<TutorialDragGuideCue>());

        public readonly int Revision;
        public readonly IReadOnlyList<TutorialOutlineCue> Outlines;
        public readonly IReadOnlyList<TutorialDragGuideCue> DragGuides;

        public TutorialOverlayViewModel(int revision, IReadOnlyList<TutorialOutlineCue> outlines, IReadOnlyList<TutorialDragGuideCue> dragGuides)
        {
            Revision = revision;
            Outlines = outlines;
            DragGuides = dragGuides;
        }
    }
}
