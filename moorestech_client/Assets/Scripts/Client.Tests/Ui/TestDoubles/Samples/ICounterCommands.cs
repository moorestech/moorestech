namespace Client.Tests.Ui.TestDoubles.Samples
{
    /// <summary>
    /// 雛形の例で View が呼ぶコマンド口（画面レーンの IXxxCommands に当たる）
    /// Command port the view calls in the template example (stands for a screen lane's IXxxCommands)
    /// </summary>
    public interface ICounterCommands
    {
        void Increment();
    }

    // 引数の無いコマンドなので記録の型は enum にする
    // The command has no arguments, so the recorded type is an enum
    public enum CounterCommand
    {
        Increment,
    }
}
