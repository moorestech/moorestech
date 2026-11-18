namespace Client.Tests.Ui.TestDoubles.Samples
{
    /// <summary>
    /// 雛形の例の記録係。View が呼んだ Increment を Issued に積む
    /// Recorder of the template example; stacks the view's Increment calls onto Issued
    /// </summary>
    public sealed class RecordingCounterCommands : CommandRecorder<CounterCommand>, ICounterCommands
    {
        public void Increment()
        {
            Record(CounterCommand.Increment);
        }
    }
}
