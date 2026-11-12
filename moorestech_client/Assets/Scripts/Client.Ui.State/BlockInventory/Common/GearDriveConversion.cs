using Client.Game.InGame.Block;
using Game.Gear.Common;
using Mooresmaster.Model.BlocksModule;

namespace Client.Ui.State.BlockInventory.Common
{
    /// <summary>
    /// 歯車の状態とマスタの param から歯車駆動の種類を読む 1 か所（族・機械の Model、特殊族の Reader、Web ホストの DTO が共有する）。純関数で、異常はログでなく結果に載せ、どう縮退するかは呼び手が決める
    /// The single reader of the gear-drive kind from the gear state and master param (shared by family/machine models, the special reader and the Web host DTO); pure: an anomaly is returned in the result and each caller chooses its fallback
    /// </summary>
    public static class GearDriveConversion
    {
        public static GearDriveReading Read(BlockGameObject block, object param)
        {
            return Read(block.GetStateDetail<GearStateDetail>(GearStateDetail.BlockStateDetailKey), param);
        }

        public static GearDriveReading Read(GearStateDetail gear, object param)
        {
            // 歯車の詳細が無ければ電力駆動、役割はサーバーの GearStateDetail.Role に従う
            // No gear state detail means electric; the role follows the server-sent GearStateDetail.Role
            if (gear == null) return new GearDriveReading(GearDriveKind.Electric, null, null);
            if (gear.Role == GearRole.Generator) return new GearDriveReading(GearDriveKind.Generator, null, null);

            // 消費側なのに消費の param が無いのはマスタの食い違い
            // A consumer without a consumption param is a master mismatch
            if (param is IGearConsumptionParam consumption) return new GearDriveReading(GearDriveKind.Consumer, consumption, null);
            return new GearDriveReading(GearDriveKind.InconsistentConsumer, null, $"Server reports GearRole.Consumer but block param {param?.GetType().Name} has no IGearConsumptionParam");
        }
    }

    public enum GearDriveKind
    {
        Electric,
        Generator,
        Consumer,
        InconsistentConsumer,
    }

    public readonly struct GearDriveReading
    {
        public readonly GearDriveKind Kind;
        // 消費側の消費の値。Consumer 以外は null
        // The consumer's consumption param; null unless Consumer
        public readonly IGearConsumptionParam ConsumptionParam;
        // 異常の文面。InconsistentConsumer 以外は null
        // Anomaly text; null unless InconsistentConsumer
        public readonly string Anomaly;

        // 歯車で動くか（食い違いは Web と同じく電力扱い）
        // Whether gear-driven (a mismatch counts as electric, as on the Web)
        public bool IsGearDriven => Kind == GearDriveKind.Generator || Kind == GearDriveKind.Consumer;

        internal GearDriveReading(GearDriveKind kind, IGearConsumptionParam consumptionParam, string anomaly)
        {
            Kind = kind;
            ConsumptionParam = consumptionParam;
            Anomaly = anomaly;
        }
    }
}
