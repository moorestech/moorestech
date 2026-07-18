import { CloseButton, Group, Title } from "@mantine/core";
import { useTopic, dispatchAction, Topics, UiStateNames } from "@/bridge";
import { SlotGrid } from "@/shared/ui";
import BuildMenuSlot from "./BuildMenuSlot";
import { selectPayload, deletePayload } from "./buildMenuLogic";
import styles from "./style.module.css";

// uGUI BuildMenuView の web 版。エントリ選択は build_menu.select で Unity の消費キューへ届く。
// Web version of uGUI BuildMenuView; selections reach Unity's consume queue via build_menu.select.
export function BuildMenuPanel() {
  const data = useTopic(Topics.buildMenu);

  // 閉じるは既存許可済みの GameScreen 遷移要求（BlockInventoryPanel と同型）。B/ESC は Unity 側が処理する。
  // Close requests the already-allowed GameScreen transition (same as BlockInventoryPanel); B/ESC are handled by Unity.
  const close = () => void dispatchAction("ui_state.request", { state: UiStateNames.gameScreen });

  if (!data) return null;

  return (
    <div className={styles.panel} data-testid="build-menu-panel">
      <Group justify="space-between">
        <Title order={2} size="h4">ビルドメニュー</Title>
        <CloseButton data-testid="build-menu-close" onClick={close} />
      </Group>
      <div className={styles.scroll}>
        <SlotGrid cols={10} testId="build-menu-grid">
          {data.entries.map((entry) => (
            <BuildMenuSlot
              key={`${entry.entryType}:${entry.entryKey}`}
              entry={entry}
              onLeftClick={() => void dispatchAction("build_menu.select", selectPayload(entry))}
              onRightClick={
                entry.entryType === "blueprint"
                  ? () => void dispatchAction("blueprint.delete", deletePayload(entry.entryKey))
                  : undefined
              }
            />
          ))}
        </SlotGrid>
      </div>
    </div>
  );
}
