using Client.Common;
using NUnit.Framework;
using UnityEngine;

namespace Client.Tests.Control
{
    /// <summary>
    ///     接続線レイヤーの名前と番号を固定するテスト
    ///     Test pinning the connection-line layer name and index
    /// </summary>
    public class ConnectionLineLayerTest
    {
        [Test]
        public void ConnectionLineLayerReplacesElectricWireLayer()
        {
            // 旧電線と同じ番号11を維持
            // Renamed in place, keeping index 11
            Assert.AreEqual(11, LayerMask.NameToLayer("ConnectionLine"));
            Assert.AreEqual(-1, LayerMask.NameToLayer("ElectricWire"));
            Assert.AreEqual(1 << 11, LayerConst.ConnectionLineOnlyLayerMask);
        }
    }
}
