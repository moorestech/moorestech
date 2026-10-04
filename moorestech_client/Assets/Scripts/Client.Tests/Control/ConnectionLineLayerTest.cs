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
            // 旧ElectricWireと同じ番号11のまま改名されている
            // Renamed in place, keeping the old ElectricWire index 11
            Assert.AreEqual(11, LayerMask.NameToLayer("ConnectionLine"));
            Assert.AreEqual(-1, LayerMask.NameToLayer("ElectricWire"));
            Assert.AreEqual(1 << 11, LayerConst.ConnectionLineOnlyLayerMask);
        }
    }
}
