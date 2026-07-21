using NUnit.Framework;
using Tests.Module;

// namespaceを付けないことでアセンブリ内の全テストを包むSetUpFixtureになる
// Omitting the namespace makes this SetUpFixture wrap every test in the assembly
[SetUpFixture]
public class ServerTestsDebugParametersIsolationFixture
{
    private DebugParametersIsolationScope _isolationScope;

    [OneTimeSetUp]
    public void IsolateDebugParameters()
    {
        _isolationScope = DebugParametersIsolationScope.Begin("server-tests");
    }

    [OneTimeTearDown]
    public void RestoreDebugParameters()
    {
        // ドメインリロードでインスタンスが失われても環境変数から後始末できるようにする
        // Fall back to env-var-based cleanup when a domain reload has destroyed this instance
        if (_isolationScope != null) _isolationScope.End();
        else DebugParametersIsolationScope.EndOrphaned();
    }
}
