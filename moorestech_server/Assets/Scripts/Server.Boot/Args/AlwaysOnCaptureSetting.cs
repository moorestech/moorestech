using UnityEngine;

namespace Server.Boot
{
    // 常時記録（サーバーのスナップショットリング＋パケットログ、クライアントの録画リング）を動かすかの唯一の決定
    // The single decision on whether always-on capture runs: the server's snapshot ring plus packet log, and the client's recording ring
    // 起動経路ごとにスイッチを持つと片方の書き忘れで無断で録り始めるため、決定はこの型1つに集める
    // A switch per boot path lets one forgotten line start recording unannounced, so the decision lives only in this type
    public readonly struct AlwaysOnCaptureSetting
    {
        public const string DisabledReason = "常時記録を有効にしていない起動経路のため、録画リングとスナップショットリングを動かしません";

        public bool IsEnabled { get; }

        private AlwaysOnCaptureSetting(bool isEnabled)
        {
            IsEnabled = isEnabled;
        }

        // 既定無効。有人の明示経路だけが有効化する
        // Disabled by default; only an explicit attended path enables it
        public static AlwaysOnCaptureSetting Current { get; private set; } = Disabled();

        public static AlwaysOnCaptureSetting Enabled()
        {
            return new AlwaysOnCaptureSetting(true);
        }

        public static AlwaysOnCaptureSetting Disabled()
        {
            return new AlwaysOnCaptureSetting(false);
        }

        // プロセス寿命全体に効く起動時の決定なので静的に持つ。サーバーとクライアントは同じ決定を読む
        // A process-lifetime boot decision, hence static; server and client read the very same decision
        public static void Apply(AlwaysOnCaptureSetting setting)
        {
            Current = setting;
            Debug.Log($"[AlwaysOnCaptureSetting] 常時記録 enabled:{setting.IsEnabled}");
        }
    }
}
