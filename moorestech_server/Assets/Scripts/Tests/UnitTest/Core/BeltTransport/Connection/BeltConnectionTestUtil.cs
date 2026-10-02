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

        // 段階4の前進と搬出を1回実行する
        // Run the stage-4 advance and output once
        public static void AdvanceAndTransfer(BeltConveyorSegment segment)
        {
            RequireMethod(typeof(BeltConveyorSegment), AdvanceAndTransferMethodName).Invoke(segment, Array.Empty<object>());
        }

        private static MethodInfo RequireMethod(Type type, string name, params Type[] parameterTypes)
        {
            var method = type.GetMethod(name, NonPublicInstanceFlags, null, parameterTypes, null);
            Assert.IsNotNull(method, $"{type.Name}.{name}({string.Join(", ", (object[])parameterTypes)}) not found via reflection");
            return method;
        }
    }
}
