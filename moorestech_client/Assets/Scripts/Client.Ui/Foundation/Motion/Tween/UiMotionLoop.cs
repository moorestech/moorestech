namespace Client.Ui.Foundation.Motion.Tween
{
    /// <summary>
    /// tween の繰り返し方。Once は尺で終わり、Repeat と PingPong は取り消すまで続く
    /// How a tween repeats; Once ends at its duration, Repeat and PingPong continue until cancelled
    /// </summary>
    public enum UiMotionLoop
    {
        Once,
        Repeat,
        PingPong,
    }
}
