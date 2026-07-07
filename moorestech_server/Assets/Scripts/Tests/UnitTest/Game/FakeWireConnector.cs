using System.Collections.Generic;
using Core.Master;
using Game.Block.Interface;
using Game.EnergySystem;

namespace Tests.UnitTest.Game
{
    /// <summary>
    /// IElectricWireConnectorのテスト用実装。ワールド初期化なしで純粋にグラフ構造だけを模擬する
    /// Test double for IElectricWireConnector that mocks pure graph structure without world bootstrap
    /// </summary>
    public class FakeWireConnector : IElectricWireConnector
    {
        // BlockInstanceIdから実体を引くための静的レジストリ。TryAddWireConnectionが相手参照を解決するのに使う
        // Static registry resolving BlockInstanceId to instance; used by TryAddWireConnection to look up the partner
        private static readonly Dictionary<BlockInstanceId, FakeWireConnector> Registry = new();

        private readonly Dictionary<BlockInstanceId, (IElectricWireConnector Connector, ElectricWireConnectionCost Cost)> _wireConnections = new();

        private FakeWireConnector(BlockInstanceId blockInstanceId)
        {
            BlockInstanceId = blockInstanceId;
            Registry[blockInstanceId] = this;
        }

        public BlockInstanceId BlockInstanceId { get; }
        public float MaxWireLength => 10f;
        public bool IsWireConnectionFull => false;
        public bool IsDestroy { get; private set; }

        public IElectricConsumer WireConsumer { get; private set; }
        public IElectricGenerator WireGenerator { get; private set; }
        public IElectricTransformer WireTransformer { get; private set; }

        public IReadOnlyDictionary<BlockInstanceId, (IElectricWireConnector Connector, ElectricWireConnectionCost Cost)> WireConnections => _wireConnections;

        public static FakeWireConnector CreateTransformer(int id)
        {
            var connector = new FakeWireConnector(new BlockInstanceId(id));
            connector.WireTransformer = new FakeElectricTransformer(connector.BlockInstanceId);
            return connector;
        }

        public static FakeWireConnector CreateGenerator(int id)
        {
            var connector = new FakeWireConnector(new BlockInstanceId(id));
            connector.WireGenerator = new FakeElectricGenerator(connector.BlockInstanceId);
            return connector;
        }

        public static FakeWireConnector CreateConsumer(int id)
        {
            var connector = new FakeWireConnector(new BlockInstanceId(id));
            connector.WireConsumer = new FakeElectricConsumer(connector.BlockInstanceId);
            return connector;
        }

        // 双方向にワイヤー接続を張るテスト用ヘルパー
        // Test helper wiring a bidirectional connection between two fakes
        public static void ConnectEachOther(FakeWireConnector a, FakeWireConnector b)
        {
            var cost = new ElectricWireConnectionCost(new ItemId(1), 1);
            a.TryAddWireConnection(b.BlockInstanceId, cost);
            b.TryAddWireConnection(a.BlockInstanceId, cost);
        }

        // 双方向のワイヤー接続を解除するテスト用ヘルパー
        // Test helper tearing down a bidirectional connection between two fakes
        public static void DisconnectEachOther(FakeWireConnector a, FakeWireConnector b)
        {
            a.TryRemoveWireConnection(b.BlockInstanceId, out _);
            b.TryRemoveWireConnection(a.BlockInstanceId, out _);
        }

        // テスト間でIDが漏れて偽成功しないよう、静的レジストリを空にする
        // Empties the static registry so IDs don't leak across tests and cause false positives
        public static void ClearRegistry()
        {
            Registry.Clear();
        }

        public bool ContainsWireConnection(BlockInstanceId partnerId)
        {
            return _wireConnections.ContainsKey(partnerId);
        }

        public bool TryAddWireConnection(BlockInstanceId partnerId, ElectricWireConnectionCost cost)
        {
            if (_wireConnections.ContainsKey(partnerId)) return false;
            if (!Registry.TryGetValue(partnerId, out var partner)) return false;
            _wireConnections[partnerId] = (partner, cost);
            return true;
        }

        public bool TryRemoveWireConnection(BlockInstanceId partnerId, out ElectricWireConnectionCost cost)
        {
            if (_wireConnections.TryGetValue(partnerId, out var entry))
            {
                cost = entry.Cost;
                _wireConnections.Remove(partnerId);
                return true;
            }

            cost = default;
            return false;
        }

        public void Destroy()
        {
            IsDestroy = true;
        }
    }
}
