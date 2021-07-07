using System;
using System.Collections.Generic;
using industrialization.Core;
using industrialization.Core.Installation.Machine.util;
using industrialization.OverallManagement.DataStore;
using industrialization.Server.Util;

namespace industrialization.Server.PacketResponse.ProtocolImplementation
{
    public static class PutInstallationProtocol
    {
        public static byte[] GetResponse(byte[] payload)
        {
            //パケットのパース、接続元、接続先のインスタンス取得
            var payloadData = new ByteArrayEnumerator(payload);
            payloadData.MoveNextToGetShort();
            int installationId = payloadData.MoveNextToGetInt();
            payloadData.MoveNextToGetShort();
            int x = payloadData.MoveNextToGetInt();
            int y = payloadData.MoveNextToGetInt();
            
            var inputInstallation = WorldInstallationInventoryDatastore.GetInstallation(payloadData.MoveNextToGetInt());

            var installation = NormalMachineFactory.Create(installationId, IntId.NewIntId(), WorldInstallationInventoryDatastore.GetInstallation(payloadData.MoveNextToGetInt()));
            inputInstallation.ChangeConnector(installation);
            
            WorldInstallationInventoryDatastore.AddInstallation(installation,installation.IntId);
            WorldInstallationDatastore.AddInstallation(installation, x, y);
            //返すものはない
            return Array.Empty<byte>();
        }
    }
}