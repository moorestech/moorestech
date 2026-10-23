import { Stack, Text } from "@mantine/core";
import { dispatchAction } from "@/bridge";
import type { BlockInventoryOpen } from "@/bridge";
import { L, useI18n } from "@/shared/i18n";
import { GaugeBar, PanelActionButton } from "@/shared/ui";
import LackHighlightText from "../LackHighlightText";
import { spaceElevatorStatusDisplay } from "./spaceElevatorDisplay";

// 接続状態・プール残量・昇降の進みと、未接続なら接続ボタンを出す。接続できるかはサーバーが status で渡す
// Show the connection state, the pool amount and the climb progress, plus the connect button while unconnected; the server passes connectability in status
export default function SpaceElevatorSection({ data }: { data: BlockInventoryOpen }) {
  const { t } = useI18n();
  const detail = data.spaceElevator;
  // silent-ok: 詳細未着は表示を保留する / C# DTOビルダーが未着を警告する
  if (!detail) return null;
  const display = spaceElevatorStatusDisplay(detail.status);
  const canConnect = detail.status === "readyToConnect";
  const connect = () => {
    void dispatchAction("space_elevator.connect", {});
  };

  return (
    <Stack gap={4} data-testid="space-elevator-section">
      <LackHighlightText insufficient={display.insufficient} size="sm" testId="space-elevator-status">{t(display.labelKey)}</LackHighlightText>
      <Text size="sm" c="var(--text-default)" data-testid="space-elevator-pool">
        {t(L.ui.blockInventory.spaceElevatorPool, { pool: detail.cablePoolAmount, required: detail.cablesPerElevator })}
      </Text>
      {detail.connected ? <GaugeBar value={detail.climbProgress} testId="space-elevator-progress" /> : null}
      {detail.connected ? null : (
        <PanelActionButton onClick={connect} disabled={!canConnect} testId="space-elevator-connect">
          {t(L.ui.blockInventory.spaceElevatorConnect)}
        </PanelActionButton>
      )}
    </Stack>
  );
}
