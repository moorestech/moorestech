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
            // 詳細が無ければ電力駆動
            // No detail means electric
            if (gear == null) return GearDriveReading.Electric();
            if (gear.Role == GearRole.Generator) return GearDriveReading.Generator();

            // 消費側の param 無しは食い違い
            // Consumer without param is a master mismatch
            if (param is IGearConsumptionParam consumption) return GearDriveReading.Consumer(consumption);
            return GearDriveReading.InconsistentConsumer($"Server reports GearRole.Consumer but block param {param?.GetType().Name} has no IGearConsumptionParam");
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
        private readonly IGearConsumptionParam _consumptionParam;
        private readonly string _anomaly;

        // 歯車駆動か（食い違いは電力扱い）
        // Gear-driven? (mismatch counts as electric)
        public bool IsGearDriven => Kind == GearDriveKind.Generator || Kind == GearDriveKind.Consumer;

        // 種別ごとの値は factory だけが組むので、種別と値の矛盾した組は作れない
        // Only the factories build the per-kind values, so a kind can never pair with the wrong values
        private GearDriveReading(GearDriveKind kind, IGearConsumptionParam consumptionParam, string anomaly)
        {
            Kind = kind;
            _consumptionParam = consumptionParam;
            _anomaly = anomaly;
        }

        public static GearDriveReading Electric()
        {
            return new GearDriveReading(GearDriveKind.Electric, null, null);
        }

        public static GearDriveReading Generator()
        {
            return new GearDriveReading(GearDriveKind.Generator, null, null);
        }

        public static GearDriveReading Consumer(IGearConsumptionParam consumptionParam)
        {
            return new GearDriveReading(GearDriveKind.Consumer, consumptionParam, null);
        }

        public static GearDriveReading InconsistentConsumer(string anomaly)
        {
            return new GearDriveReading(GearDriveKind.InconsistentConsumer, null, anomaly);
        }

        // 消費側のときだけ消費の値を返す
        // Returns the consumption param only for a consumer
        public bool TryGetConsumption(out IGearConsumptionParam consumptionParam)
        {
            consumptionParam = _consumptionParam;
            return Kind == GearDriveKind.Consumer;
        }

        // 食い違いのときだけ異常の文面を返す
        // Returns the anomaly text only for a mismatch
        public bool TryGetAnomaly(out string anomaly)
        {
            anomaly = _anomaly;
            return Kind == GearDriveKind.InconsistentConsumer;
        }
    }
}
