using NUnit.Framework;

namespace Tests.CombinedTest.Server.PacketTest
{
    public class GetFluidInventoryProtocol
    {
        [Test]
        public void GetFluidMachineTest()
        {
            // 機械を設置 参考: moorestech/moorestech_server/Assets/Scripts/Tests/CombinedTest/Core/MachineFluidIOTest.cs
            // 機械に液体を挿入
            // プロトコル経由で液体を取得 参考：このディレクトリの他のテスト
            // 機械の液体とプロトコルで取得した液体を比較
        }
        
        [Test]
        public void GetSteamEngineTest()
        {
            // 上の機械がSteamEngineになったバージョン 参考： moorestech/moorestech_server/Assets/Scripts/Tests/CombinedTest/Core/SteamGearGeneratorTest.cs
        }
        
        [Test]
        public void GetFluidPipeTest()
        {
            // 上の機械がパイプになったバージョン 参考: moorestech/moorestech_server/Assets/Scripts/Tests/CombinedTest/Core/FluidTest.cs
            
        }
        
    }
}