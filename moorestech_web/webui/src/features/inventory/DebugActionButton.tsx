import { Button } from "@mantine/core";
import { dispatchAction } from "@/bridge";
import { emitToast } from "@/features/toast";
import { useI18n } from "@/shared/i18n";

// debug.echo を発行して双方向APIの疎通を確認する開発用ボタン
// Dev button that sends debug.echo to verify the bidirectional API
export default function DebugActionButton() {
  const { t } = useI18n();
  const onClick = async () => {
    const ok = await dispatchAction("debug.echo", { hello: "world" });
    if (ok) emitToast("debug.echo ok", "info");
  };

  return (
    <Button variant="default" size="compact-sm" onClick={onClick}>
      {t("Ping Action")}
    </Button>
  );
}
