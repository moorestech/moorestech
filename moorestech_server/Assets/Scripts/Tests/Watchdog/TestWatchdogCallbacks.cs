using NUnit.Framework.Interfaces;
using Tests.Watchdog;
using UnityEngine.TestRunner;

[assembly: TestRunCallback(typeof(TestWatchdogCallbacks))]

public sealed class TestWatchdogCallbacks : ITestRunCallback
{
    public void RunStarted(ITest testsToRun) => TestWatchdog.EnsureStarted();
    public void RunFinished(ITestResult testResults) => TestWatchdog.Stop();
    
    public void TestStarted(ITest test)
    {
        if (test.IsSuite) return;
        var timeout = TestWatchdog.ResolveTimeout(test);
        TestWatchdog.OnTestStarted(test.FullName, timeout);
    }
    
    public void TestFinished(ITestResult result)
    {
        if (result.Test.IsSuite) return;
        TestWatchdog.OnTestFinished(result.Test.FullName);
    }
}
