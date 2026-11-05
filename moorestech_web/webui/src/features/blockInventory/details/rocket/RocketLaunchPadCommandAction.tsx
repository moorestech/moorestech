import { dispatchAction, type BlockInventoryOpen } from "@/bridge";
import { useI18n } from "@/shared/i18n";
import { PanelActionButton } from "@/shared/ui";
import { rocketCommandButton } from "./rocketLaunchPadLogic";

// 初回の合図をパネルのタイトル欄に置く
// Place the first-launch command in the panel's title action slot
export default function RocketLaunchPadCommandAction({ data }: { data: BlockInventoryOpen }) {
  const { t } = useI18n();
  const detail = data.blockType === "RocketLaunchPad" ? data.rocketLaunchPad : undefined;
  const command = detail ? rocketCommandButton(detail) : null;
  // silent-ok: 合図待ち以外は手動操作を持たない / 本文の状態と進捗で現在の段階が見える
  if (!command) return null;
  return (
    <PanelActionButton
      testId={`rocket-launch-pad-${command.value}`}
      onClick={() => { void dispatchAction("rocket_launch_pad.command", { command: command.value }); }}
    >
      {t(command.labelKey)}
    </PanelActionButton>
  );
}
