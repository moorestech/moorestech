using Core.Master;
using Game.Block.Interface;
using Tests.Module.TestMod;
using UnityEngine;

namespace Tests.Util.Throughput
{
    // 配置の組み立て補助。North=+Z・East=+X・South=-Z・West=-X で、ベルトは向きの先へ搬出する
    // Layout helpers: North=+Z, East=+X, South=-Z, West=-X; a belt emits toward its facing
    public static class BeltThroughputLayout
    {
        public static Vector3Int Step(BlockDirection direction)
        {
            switch (direction)
            {
                case BlockDirection.North: return new Vector3Int(0, 0, 1);
                case BlockDirection.East: return new Vector3Int(1, 0, 0);
                case BlockDirection.South: return new Vector3Int(0, 0, -1);
                default: return new Vector3Int(-1, 0, 0);
            }
        }

        // start から向きの先へ count マス並べ、最後のマスの次の位置を返す
        // Lays count cells from start along the facing and returns the position after the last cell
        public static Vector3Int Line(BeltThroughputScenario scenario, BlockId belt, Vector3Int start, BlockDirection direction, int count)
        {
            var position = start;
            for (var i = 0; i < count; i++)
            {
                scenario.Block(belt, position, direction);
                position += Step(direction);
            }
            return position;
        }

        // 歯車族の動力。10 RPM(基準回転数)・無限トルクなので、歯車ベルトはマスタ速度で回る
        // Power for gear families: 10 RPM (the base) with infinite torque, so gear belts run at master speed
        public static BeltThroughputScenario Generator(BeltThroughputScenario scenario, Vector3Int position)
        {
            return scenario.Block(ForUnitTestModBlockId.InfinityTorqueSimpleGearGenerator, position, BlockDirection.North);
        }
    }
}
