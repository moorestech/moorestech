using System;
using System.Collections.Generic;

namespace Client.Game.InGame.BugReport.Recording
{
    // Escape時点に確保した録画一式。録れていないときは理由だけを持ち、可否と中身が別々に観測されない
    // Everything the Escape moment secured from the recording; an unavailable one carries only a reason, never split from its contents
    public readonly struct CapturedRecording
    {
        public bool IsAvailable { get; }
        public string UnavailableReason { get; }
        public IReadOnlyList<string> SegmentFiles { get; }
        public IReadOnlyList<FrameTickRow> FrameTicks { get; }

        private CapturedRecording(bool isAvailable, string unavailableReason, IReadOnlyList<string> segmentFiles, IReadOnlyList<FrameTickRow> frameTicks)
        {
            IsAvailable = isAvailable;
            UnavailableReason = unavailableReason;
            SegmentFiles = segmentFiles;
            FrameTicks = frameTicks;
        }

        public static CapturedRecording Available(IReadOnlyList<string> segmentFiles, IReadOnlyList<FrameTickRow> frameTicks)
        {
            return new CapturedRecording(true, "", segmentFiles, frameTicks);
        }

        public static CapturedRecording Unavailable(string reason)
        {
            return new CapturedRecording(false, string.IsNullOrEmpty(reason) ? RecordingAvailability.StoppedWithoutReason : reason, Array.Empty<string>(), Array.Empty<FrameTickRow>());
        }
    }
}
