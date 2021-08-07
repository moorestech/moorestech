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
            WorldBlockDatastore.ClearData();
            
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
            PutBlock(1, -10, -5);
            PutBlock(0, -5, -5);
            PutBlock(4, -13, -12);
            PutBlock(1, -10, -5);
            PutBlock(9, 10, 10);
            PutBlock(9, 15, 10);
            PutBlock(9, 10, 20);
            //限界地分析
            PutBlock(0, -1, -1);
            PutBlock(0, -1, 4);
            PutBlock(0, 4, -1);
            PutBlock(0, 4, 4);
            
            //取得範囲内に建物を設置
            var ansBlocks = new List<Block>();
            PutBlock(1, 0, 0);
            ansBlocks.Add(new Block(0,0,1));
            PutBlock(2, 0, 3);
            ansBlocks.Add(new Block(0,3,2));
            PutBlock(3, 3, 0);
            ansBlocks.Add(new Block(3,0,3));
            PutBlock(6, 3, 3);
            ansBlocks.Add(new Block(3,3,6));
            PutBlock(10, 2, 1);
            ansBlocks.Add(new Block(2,1,10));
            PutBlock(15, 0, 2);
            ansBlocks.Add(new Block(0,2,15));
            PutBlock(30, 3, 1);
            ansBlocks.Add(new Block(3,1,30));
            
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
            var responseBlocks = new List<Block>();
            for (int i = 0; i < num; i++)
            {
                responseBlocks.Add(new Block(
                    response.MoveNextToGetInt(),
                    response.MoveNextToGetInt(),
                    response.MoveNextToGetInt()));
            }
            
            responseBlocks.Sort((a,b) => a.BlocksId-b.BlocksId);

            Console.WriteLine(responseBlocks.Count);
            //実際に正しいかテスト
            for (int i = 0; i < ansBlocks.Count; i++)
            {
                Console.WriteLine(i);
                Assert.AreEqual(ansBlocks[i].x,responseBlocks[i].x);
                Assert.AreEqual(ansBlocks[i].y,responseBlocks[i].y);
                Assert.AreEqual(ansBlocks[i].BlocksId,responseBlocks[i].BlocksId);
            }
        }
        class Block
        {
            public readonly int BlocksId;
            public readonly int x;
            public readonly int y;

            public Block(int x, int y,int BlocksId)
            {
                BlocksId = BlocksId;
                this.x = x;
                this.y = y;
            }
        }
        void PutBlock(int id,int x,int y)
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