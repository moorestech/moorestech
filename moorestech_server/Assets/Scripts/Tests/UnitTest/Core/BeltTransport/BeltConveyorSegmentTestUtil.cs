using Core.BeltTransport;
using Core.Item.Interface;
using Core.Master;

namespace Tests.UnitTest.Core.BeltTransport
{
    // segment の単体テストで共有する生成補助。segment の非公開メンバーには触れない(内部を固定するテストは 2026-10-18 に廃止、挙動は Throughput スイートが世界経由で守る)
    // Shared construction helpers for segment unit tests; nothing here touches segment internals (tests pinning internals were dropped on 2026-10-18, behavior is guarded through the world by the Throughput suite)
    public static class BeltConveyorSegmentTestUtil
    {
        public const int W = BeltConstants.ItemWidth;

        // 優先の高い順に3方向を並べた順序値
        // An order value listing three directions from the highest priority
        public static int Order(BeltDirection first, BeltDirection second, BeltDirection third)
        {
            return (int)first | ((int)second << 2) | ((int)third << 4);
        }

        // インスタンスIDに通し番号を入れ、順序の検証に使う
        // Put a serial number in the instance id to verify ordering
        public static BeltItem MakeItem(long serial)
        {
            return new BeltItem(new ItemId(1), new ItemInstanceId(serial), BeltEntryDirection.FromBack);
        }

        public static BeltNormalSegment CreateNormal(int capacity, int speed)
        {
            return new BeltNormalSegment(capacity, speed);
        }
    }
}
