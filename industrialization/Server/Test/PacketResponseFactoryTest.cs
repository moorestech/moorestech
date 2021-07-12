using System;
using System.Collections.Generic;
using industrialization.OverallManagement.DataStore;
using industrialization.Server.PacketResponse;
using industrialization.Server.PacketResponse.ProtocolImplementation;
using industrialization.Server.Util;
using NUnit.Framework;

namespace industrialization.Server.Test
{
    //クライアントからのバイト配列から正しいレスポンスが返るかのテスト
    public class PacketResponseFactoryTest
    {
        //なにも設定してないときに空の配列を取得する
        [Test]
        public void NoInstalledWhenCorrectlyDataAcquirableTest()
        {
            WorldInstallationDatastore.ClearData();
            
            var send = new List<byte>();
            send.AddRange(ByteArrayConverter.ToByteArray((short)2));
            send.AddRange(ByteArrayConverter.ToByteArray((short)0));
            send.AddRange(ByteArrayConverter.ToByteArray(0));
            send.AddRange(ByteArrayConverter.ToByteArray(0));
            var response = PacketResponseFactory.GetPacketResponse(send.ToArray());
            var ansResponse  = new List<byte>();
            ansResponse.AddRange(ByteArrayConverter.ToByteArray((short)1));
            ansResponse.AddRange(ByteArrayConverter.ToByteArray(0));
            ansResponse.AddRange(ByteArrayConverter.ToByteArray((short)0));
            ansResponse.AddRange(ByteArrayConverter.ToByteArray(0));
            ansResponse.AddRange(ByteArrayConverter.ToByteArray(0));


            for (int i = 0; i < ansResponse.Count; i++)
            {
                Console.WriteLine(i);
                Assert.AreEqual(ansResponse[i],response[0][i]);
            }
        }
        
        [Test]
        //いくつかの建物を設置して配列を取得する
        public void Building1ChunkInstalledWhenCorrectlyDataAcquirableTest()
        {
            //取得範囲外に建物設置
            PutInstallation(1, -10, -5);
            PutInstallation(5, -1, -1);
            PutInstallation(0, -5, -5);
            PutInstallation(4, -13, -12);
            PutInstallation(1, -10, -5);
            PutInstallation(9, 10, 10);
            PutInstallation(9, 15, 10);
            PutInstallation(9, 10, 20);
            
            //取得範囲内に建物を設置
            var ansInstallations = new List<Installation>();
            PutInstallation(1, 0, 0);
            ansInstallations.Add(new Installation(0,0,1));
            PutInstallation(2, 0, 5);
            ansInstallations.Add(new Installation(0,5,2));
            PutInstallation(3, 4, 6);
            ansInstallations.Add(new Installation(4,6,3));
            PutInstallation(6, 5, 0);
            ansInstallations.Add(new Installation(5,0,6));
            PutInstallation(10, 9, 9);
            ansInstallations.Add(new Installation(9,9,10));
            PutInstallation(15, 9, 0);
            ansInstallations.Add(new Installation(9,0,15));
            PutInstallation(30, 0, 9);
            ansInstallations.Add(new Installation(0,9,30));
            
            //設置データの要求
            var send = new List<byte>();
            send.AddRange(ByteArrayConverter.ToByteArray((short)2));
            send.AddRange(ByteArrayConverter.ToByteArray((short)0));
            send.AddRange(ByteArrayConverter.ToByteArray(0));
            send.AddRange(ByteArrayConverter.ToByteArray(0));
            
            //実際にレスポンスを要求
            var response = new ByteArrayEnumerator(PacketResponseFactory.GetPacketResponse(send.ToArray())[0]);
            response.MoveNextToGetShort();
            int num = response.MoveNextToGetInt();
            response.MoveNextToGetShort();
            response.MoveNextToGetInt();
            response.MoveNextToGetInt();

            //レスポンスを解析して必要なデータを取得
            var responseInstallations = new List<Installation>();
            for (int i = 0; i < num; i++)
            {
                responseInstallations.Add(new Installation(
                    response.MoveNextToGetInt(),
                    response.MoveNextToGetInt(),
                    response.MoveNextToGetInt()));
                response.MoveNextToGetInt();
            }
            
            responseInstallations.Sort((a,b) => a.InstallationsId-b.InstallationsId);

            Console.WriteLine(responseInstallations.Count);
            //実際に正しいかテスト
            for (int i = 0; i < ansInstallations.Count; i++)
            {
                Console.WriteLine(i);
                Assert.AreEqual(ansInstallations[i].x,responseInstallations[i].x);
                Assert.AreEqual(ansInstallations[i].y,responseInstallations[i].y);
                Assert.AreEqual(ansInstallations[i].InstallationsId,responseInstallations[i].InstallationsId);
            }
        }
        class Installation
        {
            public readonly int InstallationsId;
            public readonly int x;
            public readonly int y;

            public Installation(int x, int y,int installationsId)
            {
                InstallationsId = installationsId;
                this.x = x;
                this.y = y;
            }
        }
        void PutInstallation(int id,int x,int y)
        {
            var send = new List<byte>();
            send.AddRange(ByteArrayConverter.ToByteArray((short)1));
            send.AddRange(ByteArrayConverter.ToByteArray(id));
            send.AddRange(ByteArrayConverter.ToByteArray((short)0));
            send.AddRange(ByteArrayConverter.ToByteArray(x));
            send.AddRange(ByteArrayConverter.ToByteArray(y));
            send.AddRange(ByteArrayConverter.ToByteArray(Int32.MaxValue));
            send.AddRange(ByteArrayConverter.ToByteArray(Int32.MaxValue));
            var response = PacketResponseFactory.GetPacketResponse(send.ToArray());
        }
    }
}