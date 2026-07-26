using Core.Master;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;

using Server.Protocol.PacketResponse.Util.ElectricWire.AutoConnect;

namespace Tests.UnitTest.Server
{
    /// <summary>
    /// 接続数を宣言するブロックが必ずresolverで解決できることを不変条件として固定する
    /// Locks the invariant that every block declaring a wire capacity resolves through the resolver
    /// </summary>
    public class ElectricWireBlockParamResolverTest
    {
        private const string CapacityPropertyName = "MaxWireConnectionCount";

        [SetUp]
        public void SetUp()
        {
            // マスタ含むサーバーコンテキスト構築
            // Build server context with master data
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
        }

        [Test]
        public void ワイヤー接続数を宣言する全ブロックはresolverで解決できる()
        {
            // 接続数プロパティの有無はスキーマ宣言に対応し、interface付与の有無とは独立に判定できる
            // The capacity property mirrors the schema declaration, independent of whether the interface was applied
            var checkedCount = 0;

            foreach (var blockMaster in MasterHolder.BlockMaster.Blocks.Data)
            {
                var blockParam = blockMaster.BlockParam;
                if (blockParam.GetType().GetProperty(CapacityPropertyName) == null) continue;

                checkedCount++;
                var resolved = ElectricWireBlockParamResolver.TryGetWireRangeParam(blockParam, out var maxWireConnectionCount, out _, out _);

                Assert.IsTrue(resolved, $"{blockMaster.Name} は{CapacityPropertyName}を宣言しているがresolverで解決できない。blocks.ymlのimplementationInterface付与漏れを疑うこと");
                Assert.AreEqual(GetDeclaredCapacity(blockParam), maxWireConnectionCount, $"{blockMaster.Name} の接続数がマスタ宣言値と一致しない");
            }

            // 電気系ブロックが1つも走査されない構成ならテスト自体が無意味になるため件数も固定する
            // Pin the scanned count so a master without electric blocks cannot silently pass
            Assert.Greater(checkedCount, 0, "接続数を宣言するブロックが1件も見つからなかった");
        }

        private static int GetDeclaredCapacity(object blockParam)
        {
            return (int)blockParam.GetType().GetProperty(CapacityPropertyName).GetValue(blockParam);
        }
    }
}
