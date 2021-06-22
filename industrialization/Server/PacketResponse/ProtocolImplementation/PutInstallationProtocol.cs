using System;
using System.Collections.Generic;
using industrialization.Core.Installation.Machine.util;
using industrialization.OverallManagement.DataStore;

namespace industrialization.Server.PacketResponse.ProtocolImplementation
{
    public class PutInstallationProtocol
    {
        public PutInstallationProtocol(byte[] payload)
        {
            //パケットのパース、接続元、接続先のインスタンス取得
            int installationId = BitConverter.ToInt32(new byte[4] {payload[2], payload[3], payload[4], payload[5]});
            int x = BitConverter.ToInt32(new byte[4] {payload[8], payload[8], payload[10], payload[11]});
            int y = BitConverter.ToInt32(new byte[4] {payload[12], payload[13], payload[14], payload[15]});

            var guidByte = new List<byte>();
            for (int i = 16; i <= 31; i++)
            {
                guidByte.Add(payload[i]);
            }
            Guid input = new Guid(guidByte.ToArray());
            var inputInstalltion = WorldInstallationInventoryDatastore.GetInstallation(input);

            guidByte.Clear();
            guidByte = new List<byte>();
            for (int i = 16; i <= 31; i++)
            {
                guidByte.Add(payload[i]);
            }
            Guid output = new Guid(guidByte.ToArray());
            guidByte.Clear();
            
            var installtion = NormalMachineFactory.Create(installationId, Guid.NewGuid(), WorldInstallationInventoryDatastore.GetInstallation(output));
            inputInstalltion.ChangeConnector(installtion);
            
            WorldInstallationInventoryDatastore.AddInstallation(installtion,installtion.Guid);
            WorldInstallationDatastore.AddInstallation(installtion, x, y);
        }

        public static byte[] GetResponse(byte[] payload)
        {
            //返すものはない
            return Array.Empty<byte>();
        }
    }
}