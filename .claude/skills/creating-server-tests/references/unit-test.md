# UnitTestパターン

## 基本構造

```csharp
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using Game.Context;

namespace Tests.UnitTest.Core.{Category}
{
    public class {Feature}Test
    {
        [SetUp]
        public void Setup()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator()
                .Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
        }

        [Test]
        public void {テスト内容}Test()
        {
            // テストコード
        }
    }
}
```

## パラメータ化テスト

```csharp
[TestCase(1, 5, 6)]
[TestCase(2, 3, 5)]
public void AddTest(int a, int b, int expected)
{
    Assert.AreEqual(expected, a + b);
}
```
