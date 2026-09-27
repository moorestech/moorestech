using System.Runtime.CompilerServices;

// Client.Tests: RemoteExecRunnerTest が状態リセットに internal Stop() を使う（Server.Tests側へ全面移設すると
// PlayMode寄りの残りテストのSetUpが動かせなくなるため、世代テスト本体だけをServer.Tests側へ移した上でこちらは残す）
// Client.Tests still needs the internal Stop() for RemoteExecRunnerTest's state reset (moving it out entirely would strand
// the remaining PlayMode-adjacent test's SetUp, so only the generation tests themselves moved to Server.Tests)
[assembly: InternalsVisibleTo("Client.Tests")]
[assembly: InternalsVisibleTo("Server.Tests")]
