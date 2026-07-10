namespace Game.Gear.Common
{
    public interface IGearGenerator : IGear
    {
        public RPM GenerateRpm { get; }
        public Torque GenerateTorque { get; }

        public bool GenerateIsClockwise { get; }

        // 内部状態が時間で進む（燃料消費等の毎tick処理が要る）generatorかどうか
        // Whether this generator's internal state advances over time and needs per-tick processing (e.g. fuel)
        public bool RequiresContinuousTick { get; }

        // 確定済みの負荷率で毎tickの燃料消費・出力更新を行う。出力が変化したらGearNetworkDatastoreへ自己通知する
        // Consume fuel and update output each tick using the settled load rate; self-notify GearNetworkDatastore on output change
        public void ConsumeGeneratorTick(float networkLoadRate);
    }
}
