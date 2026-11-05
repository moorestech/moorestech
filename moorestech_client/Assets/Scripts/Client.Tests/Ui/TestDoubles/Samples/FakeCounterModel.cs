namespace Client.Tests.Ui.TestDoubles.Samples
{
    /// <summary>
    /// 雛形の例の偽モデル。表示する数を差し込む
    /// Fake model of the template example; pushes the count to display
    /// </summary>
    public sealed class FakeCounterModel : FakeModel<int>
    {
        public FakeCounterModel(int initialCount) : base(initialCount)
        {
        }
    }
}
