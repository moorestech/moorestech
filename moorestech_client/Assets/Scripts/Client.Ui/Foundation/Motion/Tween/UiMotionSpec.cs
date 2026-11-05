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
            DurationMilliseconds = durationMilliseconds;
            DelayMilliseconds = delayMilliseconds;
            Easing = easing;
            Loop = loop;
        }
    }
}
