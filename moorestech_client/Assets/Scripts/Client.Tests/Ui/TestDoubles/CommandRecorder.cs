using System.Collections.Generic;

namespace Client.Tests.Ui.TestDoubles
{
    /// <summary>
    /// View が発行したコマンドを発行順に記録する。テストは Issued を見て、押した操作が正しい中身で出たかを確かめる
    /// Records the commands a view issued, in order; tests read Issued to check that an operation went out with the right payload
    /// </summary>
    /// <remarks>
    /// 画面レーンの作り方: View が呼ぶ IXxxCommands を実装する RecordingXxxCommands : CommandRecorder&lt;XxxCommand&gt;, IXxxCommands を作り、各メソッドは引数を XxxCommand にして Record する。
    /// Playwright の e2e で payloadsOf(...) と書いていた箇所は、ここの Issued に置き換える。
    /// 拡張点: 引数の無いコマンドは enum、引数のあるコマンドは不変の struct を TCommand にする。最小の例は Samples/ にある。
    /// How a screen lane uses it: make RecordingXxxCommands : CommandRecorder&lt;XxxCommand&gt;, IXxxCommands implementing the commands the view calls, and have each method Record its arguments as an XxxCommand.
    /// Where a Playwright e2e wrote payloadsOf(...), read Issued here instead.
    /// Extension: use an enum as TCommand for argument-less commands and an immutable struct for commands with arguments; the minimal example lives in Samples/.
    /// </remarks>
    public class CommandRecorder<TCommand>
    {
        private readonly List<TCommand> _issued = new();

        public IReadOnlyList<TCommand> Issued => _issued;

        public void Record(TCommand command)
        {
            _issued.Add(command);
        }

        public void Clear()
        {
            _issued.Clear();
        }
    }
}
