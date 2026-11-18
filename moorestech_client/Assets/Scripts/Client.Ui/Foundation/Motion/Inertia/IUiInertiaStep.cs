namespace Client.Ui.Foundation.Motion.Inertia
{
    /// <summary>
    /// 慣性で進んだ開始からの変位を対象へ書く
    /// Writes the displacement travelled by inertia since start to the target
    /// </summary>
    public interface IUiInertiaStep
    {
        void Apply(float offset);
    }
}
