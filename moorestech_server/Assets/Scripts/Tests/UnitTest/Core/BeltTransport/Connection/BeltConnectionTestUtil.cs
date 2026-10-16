using System;
using System.Collections.Generic;
using System.Reflection;
using Core.BeltTransport;
using NUnit.Framework;

namespace Tests.UnitTest.Core.BeltTransport.Connection
{
    // 接続・段階別処理のinternalメンバーへのリフレクションをこのクラスへ集約する。各メンバーは宣言元の型で解決する
    // 本体側で名前やシグネチャが変わったら、メンバー名を含む明確な失敗として検出する
    // Collects all reflection into internal connection and per-stage members in this class, each resolved on its declaring type
    // A rename or signature change in production surfaces as a clear failure naming the member
    public static class BeltConnectionTestUtil
    {
        private const BindingFlags NonPublicInstanceFlags = BindingFlags.NonPublic | BindingFlags.Instance;
        private const string AdvanceAndTransferMethodName = "AdvanceAndTransfer";
        private const string CollectMethodName = "Collect";
        private const string TransferMethodName = "Transfer";
        private const string PriorityOrderPropertyName = "PriorityOrder";
        private const string TickSpeedPropertyName = "TickSpeed";
        private const string ResolveInputMethodName = "ResolveInput";
        private const BindingFlags NonPublicStaticFlags = BindingFlags.NonPublic | BindingFlags.Static;
        private const string TransferTypeName = "Core.BeltTransport.BeltSegmentTransfer";
        private const string CacheTransferMethodName = "CacheTransfer";
        private const string CacheMethodName = "Cache";
        private const string CaptureOfferMethodName = "CaptureOffer";
        private const string ApplyMethodName = "Apply";

        public static BeltBranchSegment CreateBranch(int capacity, int priorityOrder, BeltDirection forwardDirection)
        {
            return new BeltBranchSegment(capacity, 0, priorityOrder, forwardDirection);
        }

        public static BeltMergeSegment CreateMerge(int priorityOrder, BeltDirection forwardDirection)
        {
            return new BeltMergeSegment(0, priorityOrder, forwardDirection);
        }

        // 段階4の前進と搬出を1回実行する
        // Run the stage-4 advance and output once
        public static void AdvanceAndTransfer(BeltNormalSegment segment)
        {
            RequireMethod(typeof(BeltNormalSegment), AdvanceAndTransferMethodName).Invoke(segment, Array.Empty<object>());
        }

        // 段階2の合流予約を1回実行する
        // Run the stage-2 merge reservation once
        public static void ResolveInput(BeltMergeSegment segment)
        {
            RequireMethod(typeof(BeltMergeSegment), ResolveInputMethodName).Invoke(segment, Array.Empty<object>());
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

        // 通常segmentへの搬出を遅延反映する接続を作る。対象外ならnull
        // Create the deferred connection for output into a normal segment; null when not applicable
        public static object CacheTransfer(BeltNormalSegment segment)
        {
            return RequireMethod(typeof(BeltNormalSegment), CacheTransferMethodName).Invoke(segment, Array.Empty<object>());
        }

        // 通常segment一覧から遅延反映する接続一覧を作る
        // Build the deferred connection list from normal segments
        public static object[] CacheTransfers(BeltNormalSegment[] normal)
        {
            var transferType = RequireTransferType();
            var method = transferType.GetMethod(CacheMethodName, NonPublicStaticFlags, null, new[] { typeof(IEnumerable<BeltNormalSegment>) }, null);
            Assert.IsNotNull(method, $"{transferType.Name}.{CacheMethodName}(IEnumerable<BeltNormalSegment>) not found via reflection");
            var transfers = (Array)method.Invoke(null, new object[] { normal });
            var result = new object[transfers.Length];
            transfers.CopyTo(result, 0);
            return result;
        }

        // 段階4aの空き記録を1接続分実行する
        // Run the stage-4a offer capture for one connection
        public static void CaptureOffer(object transfer)
        {
            RequireMethod(RequireTransferType(), CaptureOfferMethodName).Invoke(transfer, Array.Empty<object>());
        }

        // 段階4cの搬入反映を1接続分実行する
        // Run the stage-4c apply for one connection
        public static void ApplyTransfer(object transfer)
        {
            RequireMethod(RequireTransferType(), ApplyMethodName).Invoke(transfer, Array.Empty<object>());
        }

        private static Type RequireTransferType()
        {
            var type = typeof(BeltConveyorSegment).Assembly.GetType(TransferTypeName);
            Assert.IsNotNull(type, $"{TransferTypeName} type not found via reflection");
            return type;
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
