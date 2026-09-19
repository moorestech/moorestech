using System.Linq;
using Client.Tests.UnitTest.CiShard.SourceScan;
using NUnit.Framework;

namespace Client.Tests.UnitTest.CiShard
{
    // メタテストの走査が境界条件で持ち主を取り違えないことを固定する
    // Pins that the meta test's scan never misattributes the owner at its boundary cases
    public class PlayModeMethodScannerTest
    {
        [Test]
        public void コメントと文字列の中のトークンは数えない()
        {
            var result = Scan(@"
namespace N
{
    // yield return new EnterPlayMode(expectDomainReload: true);
    /* new EnterPlayMode( { */
    public class C
    {
        public IEnumerator M()
        {
            var text = ""new EnterPlayMode( {"";
            var verbatim = @""new EnterPlayMode("""" }"";
            var brace = '{';
            yield return null;
        }
    }
}");
            Assert.That(result.ResolvedMethods, Is.Empty);
            Assert.That(result.Misreads, Is.Empty);
        }

        [Test]
        public void 後続やネストの宣言ではなく包んでいる型とメソッドへ帰属する()
        {
            var result = Scan(@"
namespace N
{
    public class Outer
    {
        [UnityTest, Category(""CiShardClientPlay1"")]
        public IEnumerator Run()
        {
            yield return new EnterPlayMode(expectDomainReload: true);
            IEnumerator Local() { yield return new EnterPlayMode(expectDomainReload: true); }
        }

        private class Helper
        {
            public IEnumerator Other() { yield return null; }
        }

        public class Inner
        {
            public IEnumerator Nested() { yield return new EnterPlayMode(expectDomainReload: true); }
        }
    }

    public class Later
    {
        public IEnumerator After() { yield return null; }
    }
}");
            Assert.That(result.Misreads, Is.Empty);
            var owners = result.ResolvedMethods.Select(method => method.TypeFullName + "." + method.MethodName).ToList();
            Assert.That(owners, Is.EqualTo(new[] { "N.Outer.Run", "N.Outer+Inner.Nested" }));
        }

        [Test]
        public void ジェネリック型とファイルスコープ名前空間をGetTypeで引ける名前にする()
        {
            var result = Scan(@"
namespace N.Sub;

public class Generic<TKey, TValue> where TKey : class
{
    public IEnumerator Run<T>() where T : struct { yield return new EnterPlayMode(expectDomainReload: true); }
}");
            Assert.That(result.Misreads, Is.Empty);
            Assert.That(result.ResolvedMethods.Single().TypeFullName, Is.EqualTo("N.Sub.Generic`2"));
            Assert.That(result.ResolvedMethods.Single().MethodName, Is.EqualTo("Run"));
        }

        [Test]
        public void メソッド本体の外にある出現は読み違いとして返す()
        {
            var result = Scan(@"
namespace N
{
    public class C
    {
        private readonly Func<object> _factory = () => { return new EnterPlayMode(expectDomainReload: true); };
    }
}");
            Assert.That(result.ResolvedMethods, Is.Empty);
            Assert.That(result.Misreads.Single().Line, Is.EqualTo(6));
        }

        private static PlayModeMethodScanResult Scan(string source)
        {
            var result = new PlayModeMethodScanResult();
            PlayModeMethodScanner.ScanFile("Sample.cs", source, result);
            return result;
        }
    }
}
