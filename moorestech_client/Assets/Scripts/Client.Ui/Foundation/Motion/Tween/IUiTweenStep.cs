namespace Client.Ui.Foundation.Motion.Tween
{
    /// <summary>
    /// 曲線を通した進み具合を対象の値へ書く
    /// Writes an eased progress to the target value
    /// </summary>
    public interface IUiTweenStep
    {
        void Apply(float easedProgress);
    }
}
