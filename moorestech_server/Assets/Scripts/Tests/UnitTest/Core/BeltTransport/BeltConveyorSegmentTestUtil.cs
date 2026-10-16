using System;
using System.Reflection;
using Core.BeltTransport;
using Core.Item.Interface;
using Core.Master;
using NUnit.Framework;

namespace Tests.UnitTest.Core.BeltTransport
{
    // BeltConveyorSegmentの非公開メンバーへのリフレクションをこのクラスへ集約する。宣言元の基底型で解決する
    // 本体側で名前やシグネチャが変わったら、メンバー名を含む明確な失敗として検出する
    // Collects all reflection into BeltConveyorSegment's non-public members in this class, resolved on the declaring base type
    // A rename or signature change in production surfaces as a clear failure naming the member
    public static class BeltConveyorSegmentTestUtil
    {
        public const int W = BeltConstants.ItemWidth;

        // 優先の高い順に3方向を並べた順序値
        // An order value listing three directions from the highest priority
        public static int Order(BeltDirection first, BeltDirection second, BeltDirection third)
        {
            return (int)first | ((int)second << 2) | ((int)third << 4);
        }

        private const BindingFlags PrivateInstanceFlags = BindingFlags.NonPublic | BindingFlags.Instance;
        private const string EnqueueTailMethodName = "EnqueueTail";
        private const string AdvanceMethodName = "Advance";
        private const string DequeueHeadMethodName = "DequeueHead";
        private const string BeginTickMethodName = "BeginTick";
        private const string TotalLengthPropertyName = "TotalLength";
        private const string OutputLengthPropertyName = "OutputLength";
        private const string LengthPropertyName = "Length";
        private const string BlockSizesFieldName = "_blockSizes";
        private const string HeadFieldName = "_head";

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

        public static void EnqueueTail(BeltConveyorSegment segment, int gap, BeltItem item)
        {
            // inパラメータはby-ref型として解決する
            // Resolve the in parameter as a by-ref type
            var method = RequireMethod(EnqueueTailMethodName, typeof(int), typeof(BeltItem).MakeByRefType());
            method.Invoke(segment, new object[] { gap, item });
        }

        // 速度を設定してtickを開始し、1tick前進させる
        // Set the speed, begin the tick and advance one tick
        public static void Advance(BeltConveyorSegment segment, int tickSpeed, bool sent)
        {
            BeginTickAt(segment, tickSpeed);
            RequireMethod(AdvanceMethodName, typeof(bool)).Invoke(segment, new object[] { sent });
        }

        public static void DequeueHead(BeltConveyorSegment segment)
        {
            RequireMethod(DequeueHeadMethodName).Invoke(segment, Array.Empty<object>());
        }

        public static int GetLength(BeltConveyorSegment segment)
        {
            return (int)RequireProperty(LengthPropertyName).GetValue(segment);
        }

        public static int GetTotalLength(BeltConveyorSegment segment)
        {
            return (int)RequireProperty(TotalLengthPropertyName).GetValue(segment);
        }

        // 指定速度でtickを開始した直後の搬出長
        // Output length right after beginning a tick at the given speed
        public static int GetOutputLength(BeltConveyorSegment segment, int tickSpeed)
        {
            BeginTickAt(segment, tickSpeed);
            return (int)RequireProperty(OutputLengthPropertyName).GetValue(segment);
        }

        // 出口から数えた順番orderのアイテムが持つblockSizes値
        // The blockSizes value of the item at the given order counted from the exit
        public static int GetBlockSizeAt(BeltConveyorSegment segment, int order)
        {
            var blockSizes = (int[])RequireField(BlockSizesFieldName).GetValue(segment);
            var head = (int)RequireField(HeadFieldName).GetValue(segment);
            return blockSizes[(head + order) % segment.Capacity];
        }

        public static void AssertDistances(BeltConveyorSegment segment, params int[] expectedDistances)
        {
            var states = segment.CaptureItems();
            Assert.AreEqual(expectedDistances.Length, states.Length, "count");
            for (var i = 0; i < states.Length; i++)
                Assert.AreEqual(expectedDistances[i], states[i].DistanceToExit, $"distance[{i}]");
            AssertStructure(segment);
        }

        // 占有長と密着ブロック両端のblockSizesを、捕捉した距離から導いた値と突き合わせる
        // Check the occupied length and block-end sizes against values derived from captured distances
        public static void AssertStructure(BeltConveyorSegment segment)
        {
            var states = segment.CaptureItems();
            Assert.AreEqual(segment.Count, states.Length, "count");
            var expectedTotal = states.Length == 0 ? 0 : states[states.Length - 1].DistanceToExit + W;
            Assert.AreEqual(expectedTotal, GetTotalLength(segment), "TotalLength");
            Assert.AreEqual(states.Length == 0 ? 0 : -states[0].DistanceToExit, GetOutputLength(segment, 0), "OutputLength at speed 0");

            // 隙間0で連続する区間を1ブロックとし、両端の値を検査する
            // Treat each run with zero spacing as one block and check its two ends
            var start = 0;
            for (var i = 1; i <= states.Length; i++)
            {
                var boundary = i == states.Length || states[i].DistanceToExit - states[i - 1].DistanceToExit != W;
                if (!boundary) continue;
                var size = i - start;
                Assert.AreEqual(size, GetBlockSizeAt(segment, start), $"blockSizes at block start {start}");
                Assert.AreEqual(size, GetBlockSizeAt(segment, i - 1), $"blockSizes at block end {i - 1}");
                start = i;
            }
        }

        // 速度を設定し、段階0としてこのtickの速度を固定する
        // Set the speed and fix it for this tick as stage 0
        public static void BeginTickAt(BeltConveyorSegment segment, int tickSpeed)
        {
            segment.SetSpeed(tickSpeed);
            RequireMethod(BeginTickMethodName).Invoke(segment, Array.Empty<object>());
        }

        private static MethodInfo RequireMethod(string name, params Type[] parameterTypes)
        {
            var method = typeof(BeltConveyorSegment).GetMethod(name, PrivateInstanceFlags, null, parameterTypes, null);
            Assert.IsNotNull(method, $"BeltConveyorSegment.{name}({string.Join(", ", (object[])parameterTypes)}) not found via reflection");
            return method;
        }

        private static PropertyInfo RequireProperty(string name)
        {
            var property = typeof(BeltConveyorSegment).GetProperty(name, PrivateInstanceFlags);
            Assert.IsNotNull(property, $"BeltConveyorSegment.{name} property not found via reflection");
            return property;
        }

        private static FieldInfo RequireField(string name)
        {
            var field = typeof(BeltConveyorSegment).GetField(name, PrivateInstanceFlags);
            Assert.IsNotNull(field, $"BeltConveyorSegment.{name} field not found via reflection");
            return field;
        }
    }
}
