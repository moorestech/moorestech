using System;
using System.Reflection;
using Core.BeltTransport;
using NUnit.Framework;

namespace Tests.UnitTest.Core.BeltTransport.Connection
{
    // 接続・段階別処理のinternalメンバーへのリフレクションをこのクラスへ集約する
    // 本体側で名前やシグネチャが変わったら、メンバー名を含む明確な失敗として検出する
    // Collects all reflection into internal connection and per-stage members in this class
    // A rename or signature change in production surfaces as a clear failure naming the member
    public static class BeltConnectionTestUtil
    {
        private const BindingFlags NonPublicInstanceFlags = BindingFlags.NonPublic | BindingFlags.Instance;
        private const string AdvanceAndTransferMethodName = "AdvanceAndTransfer";
        private const string CollectMethodName = "Collect";
        private const string TransferMethodName = "Transfer";
        private const string PriorityOrderPropertyName = "PriorityOrder";
        private const string TickSpeedPropertyName = "TickSpeed";

        public static BeltConveyorSegment CreateBranch(int capacity, int priorityOrder, BeltDirection forwardDirection)
        {
            return new BeltConveyorSegment(capacity, 0, BeltSegmentKind.Branch, priorityOrder, forwardDirection);
        }

        public static BeltConveyorSegment CreateMerge(int priorityOrder, BeltDirection forwardDirection)
        {
            return new BeltConveyorSegment(1, 0, BeltSegmentKind.Merge, priorityOrder, forwardDirection);
        }

        // 優先の高い順に3方向を並べた順序値
        // An order value listing three directions from the highest priority
        public static int Order(BeltDirection first, BeltDirection second, BeltDirection third)
        {
            return (int)first | ((int)second << 2) | ((int)third << 4);
        }

        // 段階4の前進と搬出を1回実行する
        // Run the stage-4 advance and output once
        public static void AdvanceAndTransfer(BeltConveyorSegment segment)
        {
            RequireMethod(typeof(BeltConveyorSegment), AdvanceAndTransferMethodName).Invoke(segment, Array.Empty<object>());
        }

        public static int GetTickSpeed(BeltConveyorSegment segment)
        {
            return (int)RequireProperty(typeof(BeltConveyorSegment), TickSpeedPropertyName).GetValue(segment);
        }

        // 段階1の回収を1回実行する
        // Run the stage-1 collect once
        public static void Collect(BeltBuffer buffer)
        {
            RequireMethod(typeof(BeltBuffer), CollectMethodName).Invoke(buffer, Array.Empty<object>());
        }

        // 段階3の搬出を1回実行する
        // Run the stage-3 transfer once
        public static void Transfer(BeltBuffer buffer)
        {
            RequireMethod(typeof(BeltBuffer), TransferMethodName).Invoke(buffer, Array.Empty<object>());
        }

        public static int GetBufferPriorityOrder(BeltBuffer buffer)
        {
            return (int)RequireProperty(typeof(BeltBuffer), PriorityOrderPropertyName).GetValue(buffer);
        }

        private static MethodInfo RequireMethod(Type type, string name, params Type[] parameterTypes)
        {
            var method = type.GetMethod(name, NonPublicInstanceFlags, null, parameterTypes, null);
            Assert.IsNotNull(method, $"{type.Name}.{name}({string.Join(", ", (object[])parameterTypes)}) not found via reflection");
            return method;
        }

        private static PropertyInfo RequireProperty(Type type, string name)
        {
            var property = type.GetProperty(name, NonPublicInstanceFlags);
            Assert.IsNotNull(property, $"{type.Name}.{name} property not found via reflection");
            return property;
        }
    }
}
