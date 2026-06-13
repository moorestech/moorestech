using Game.Block.Blocks.Machine;
using System.Reflection;

namespace Tests.Util
{
    // VanillaMachineProcessorComponentの非公開状態をテストから参照/設定するリフレクションUtil
    // Reflection util to read/write the non-public state of VanillaMachineProcessorComponent from tests
    public static class VanillaMachineProcessorTestUtil
    {
        private const BindingFlags InstanceFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

        // 残りtick数。公開されておらず_processingStateが保持する
        // Remaining ticks; not exposed and held by _processingState
        public static uint GetRemainingTicks(this VanillaMachineProcessorComponent processor)
        {
            return (uint)GetPropertyValue(GetProcessingState(processor), "RemainingTicks");
        }

        public static void SetRemainingTicks(this VanillaMachineProcessorComponent processor, uint ticks)
        {
            SetPropertyValue(GetProcessingState(processor), "RemainingTicks", ticks);
        }

        // 加工ステートのsetterは非公開のため、セーブ復元テスト用にリフレクションで設定する
        // The process-state setter is non-public, so set it via reflection for save-restore tests
        public static void SetCurrentState(this VanillaMachineProcessorComponent processor, ProcessState state)
        {
            SetPropertyValue(processor, "CurrentState", state);
        }

        private static object GetProcessingState(VanillaMachineProcessorComponent processor)
        {
            return typeof(VanillaMachineProcessorComponent)
                .GetField("_processingState", InstanceFlags)
                .GetValue(processor);
        }

        private static object GetPropertyValue(object target, string propertyName)
        {
            return target.GetType().GetProperty(propertyName, InstanceFlags).GetValue(target);
        }

        private static void SetPropertyValue(object target, string propertyName, object value)
        {
            target.GetType().GetProperty(propertyName, InstanceFlags).SetValue(target, value);
        }
    }
}
