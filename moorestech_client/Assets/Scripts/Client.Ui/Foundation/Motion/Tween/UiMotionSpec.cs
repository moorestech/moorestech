namespace Client.Ui.Foundation.Motion.Tween
{
    /// <summary>
    /// tween の尺・遅延・曲線・繰り返し方
    /// Duration, delay, easing and loop of a tween
    /// </summary>
    public sealed class UiMotionSpec
    {
        public readonly float DurationMilliseconds;
        public readonly float DelayMilliseconds;
        public readonly UiEasing Easing;
        public readonly UiMotionLoop Loop;

        public UiMotionSpec(float durationMilliseconds, float delayMilliseconds, UiEasing easing, UiMotionLoop loop)
        {
            // 非有限の尺・遅延は Once でも終わらない tween になるので作らせない
            // A non-finite duration or delay would make a Once tween that never ends, so refuse to build it
            if (!float.IsFinite(durationMilliseconds)) throw new System.ArgumentOutOfRangeException(nameof(durationMilliseconds), durationMilliseconds, "UiMotionSpec duration must be finite");
            if (!float.IsFinite(delayMilliseconds)) throw new System.ArgumentOutOfRangeException(nameof(delayMilliseconds), delayMilliseconds, "UiMotionSpec delay must be finite");
            DurationMilliseconds = durationMilliseconds;
            DelayMilliseconds = delayMilliseconds;
            Easing = easing;
            Loop = loop;
        }
    }
}
