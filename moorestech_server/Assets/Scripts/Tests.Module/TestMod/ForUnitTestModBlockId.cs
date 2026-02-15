using System;
using Core.Master;

namespace Tests.Module.TestMod
{
    /// <summary>
    /// テスト用ブロック ID 一覧。
    /// 数値リテラルを排し、blocks.json に記載された GUID から
    /// MasterHolder.BlockMaster 経由で動的に解決する。
    /// </summary>
    public static class ForUnitTestModBlockId
    {
        public static BlockId GetBlock(string guid) => MasterHolder.BlockMaster.GetBlockId(Guid.Parse(guid));
        
        public static BlockId MachineId => GetBlock("00000000-0000-0000-0000-000000000001");
        public static BlockId BlockId => GetBlock("00000000-0000-0000-0000-000000000002");
        public static BlockId BeltConveyorId => GetBlock("00000000-0000-0000-0000-000000000003");
        public static BlockId ElectricPoleId => GetBlock("00000000-0000-0000-0000-000000000004");
        public static BlockId GeneratorId => GetBlock("00000000-0000-0000-0000-000000000005");
        public static BlockId ElectricMinerId => GetBlock("00000000-0000-0000-0000-000000000006");
        public static BlockId ChestId => GetBlock("00000000-0000-0000-0000-000000000007");
        public static BlockId InfinityGeneratorId => GetBlock("00000000-0000-0000-0000-000000000008");
        
        // ★ Multi-block 系は現状 MultiBlock2 を採用（guid …000a）
        public static BlockId MultiBlockGeneratorId => GetBlock("00000000-0000-0000-0000-00000000000a");
        
        public static BlockId SmallGear => GetBlock("00000000-0000-0000-0000-00000000000c");
        public static BlockId BigGear => GetBlock("00000000-0000-0000-0000-00000000000d");
        public static BlockId Shaft => GetBlock("00000000-0000-0000-0000-00000000000e");
        public static BlockId GearMachine => GetBlock("00000000-0000-0000-0000-00000000000f");
        public static BlockId SimpleGearGenerator => GetBlock("00000000-0000-0000-0000-000000000010");
        public static BlockId SimpleFastGearGenerator => GetBlock("00000000-0000-0000-0000-000000000011");
        public static BlockId Teeth10RequireTorqueTestGear => GetBlock("00000000-0000-0000-0000-000000000012");
        public static BlockId Teeth20RequireTorqueTestGear => GetBlock("00000000-0000-0000-0000-000000000013");
        public static BlockId InfinityTorqueSimpleGearGenerator => GetBlock("00000000-0000-0000-0000-000000000014");
        public static BlockId GearBeltConveyor => GetBlock("00000000-0000-0000-0000-000000000015");
        public static BlockId GearBeltConveyorSplitter => GetBlock("eccb9f59-4439-4caf-9ae8-67da50549040");
        
        public static BlockId MachineRecipeTest1 => GetBlock("00000000-0000-0000-0000-000000000019");
        public static BlockId MachineRecipeTest2 => GetBlock("00000000-0000-0000-0000-00000000001a");
        public static BlockId MachineRecipeTest3 => GetBlock("00000000-0000-0000-0000-00000000001b");
        
        public static BlockId GearMiner => GetBlock("00000000-0000-0000-0000-00000000001c");
        
        public static BlockId TestTrainRail => GetBlock("00000000-0000-0000-0000-000000000024");
        public static BlockId TestTrainStation => GetBlock("00000000-0000-0000-0000-000000000025");
        public static BlockId TestTrainCargoPlatform => GetBlock("00000000-0000-0000-0000-000000000026");
        
        public static BlockId GearMapObjectMiner => GetBlock("00000000-0000-0000-0000-000000000027");
        
        public static BlockId FluidPipe => GetBlock("9CE688A4-8985-40B6-AD52-0F98F3BAF55E");
        public static BlockId OneWayFluidPipe => GetBlock("BCCD23DE-053E-4A54-9E32-31120B051DCA");
        
        public static BlockId FluidMachineId => GetBlock("9b36e317-b6eb-441a-b5bf-8aa99e9216a0");
        public static BlockId FuelGearGeneratorId => GetBlock("cc3b5cbe-c5bc-4d3d-b3df-4b69e7372471");
        
        public static BlockId BaseCamp1 => GetBlock("9af7370b-9982-4cdb-bee2-c8d4545581af"); // 単一アイテム要求
        public static BlockId BaseCamp2 => GetBlock("9e18bb3d-b821-4f19-a47e-e3718c39eafa"); // 複数アイテム要求
        public static BlockId TransformedBlock => GetBlock("cda19f55-adef-41f4-9212-3ed5f4f926b9"); // 変換後のブロック
        public static BlockId GearPump => GetBlock("13806455-551c-4399-a7e7-b45a95709a6b");
        public static BlockId TestGearElectricGenerator => GetBlock("00000000-0000-0000-0000-000000000028");
        public static BlockId ElectricPump => GetBlock("3829088a-5a78-43d7-8c3c-d3e4bb91b90a");
        public static BlockId GearChainPole => GetBlock("00000000-0000-0000-0000-00000000002c");
    }
}
