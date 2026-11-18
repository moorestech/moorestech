namespace Client.Ui.Foundation.Motion.Core
{
    /// <summary>
    /// UiMotion が経過時間を測る時計。本番はパネルのスケジューラ、テストは手で進める時計を注入する
    /// Clock UiMotion measures elapsed time with; production injects the panel scheduler, tests inject a manually advanced clock
    /// </summary>
    public interface IUiMotionClock
    {
        long GetNowMilliseconds();
    }
}
