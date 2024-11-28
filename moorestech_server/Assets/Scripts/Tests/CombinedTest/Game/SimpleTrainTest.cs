using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;

namespace Tests.CombinedTest.Game
{
    public class SimpleTrainTest
    {
        [Test]
        // レールに乗っている列車が指定された駅に向かって移動するテスト
        // A test in which a train on rails moves towards a designated station
        public void SimpleTrainMoveTest()
        {
            var (_, saveServiceProvider) = new MoorestechServerDIContainerGenerator().Create(TestModDirectory.ForUnitTestModDirectory);
            
            // TODO レールブロック1を設置
            // TODO レールブロック2を設置
            // TODO レールブロック同士がつながっていることを確認
            
            // TODO レールの両端に駅を設置
            
            // TODO レールに動力車1台を設置
            // TODO 列車に指定された駅に行くように指示
            
            // TODO 列車が駅に到着するまで待つ
            
            // TODO 列車が駅に到着すればpass、指定時間以内に到着しなければfail
        }
    }
}