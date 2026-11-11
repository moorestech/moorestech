namespace Client.Ui.Screens.Notification.Parts
{
    /// <summary>
    /// 行の入場・退場の尺と生存尺とずれ。入退場とずれはトークン、生存尺は NotificationModel の定数から来る
    /// Enter and exit durations, the lifetime and the shift of a row; enter, exit and shift come from tokens, the lifetime from the NotificationModel constant
    /// </summary>
    public sealed class NotificationRowTiming
    {
        public readonly float EnterMilliseconds;
        public readonly float ExitMilliseconds;
        public readonly float LifetimeMilliseconds;
        public readonly float ShiftPixels;

        public NotificationRowTiming(float enterMilliseconds, float exitMilliseconds, float lifetimeMilliseconds, float shiftPixels)
        {
            EnterMilliseconds = enterMilliseconds;
            ExitMilliseconds = exitMilliseconds;
            LifetimeMilliseconds = lifetimeMilliseconds;
            ShiftPixels = shiftPixels;
        }
    }
}
