import { CloseButton, Group, Paper, Title } from "@mantine/core";
import { useTopic, Topics, UiStateNames, dispatchAction } from "@/bridge";
import { resolveBlockComponent } from "./blockComponentRegistry";
import styles from "./style.module.css";

// ブロック UI のオーバーレイ。uGUI の SubInventoryState 相当で、blockType から中身を静的解決する
// Block UI overlay; the SubInventoryState equivalent, statically resolving the body from blockType
export default function BlockInventoryPanel() {
  const data = useTopic(Topics.blockInventory);
  // 未受信または閉状態なら何も描画しない（ブロック UI が開いていない）
  // Render nothing when not received or closed (no block UI is open)
  if (!data || !data.open) return null;

  // blockType に対応するコンポーネントを解決（未登録は GenericBlockInventory にフォールバック）
  // Resolve the component for blockType (unregistered falls back to GenericBlockInventory)
  const Body = resolveBlockComponent(data.blockType);

  return (
    <Paper data-testid="block-inventory" className={styles.panel} p="md" withBorder bg="dark.6" c="dark.1">
        <Group justify="space-between" mb="sm">
          <Title order={2} size="h4">{data.blockName}</Title>
          {/* uGUIのEsc/Tab相当のマウス閉じ操作。GameScreenへの遷移をhostへ要求する */}
          {/* Mouse-driven close, like uGUI Esc/Tab; asks the host to transit to GameScreen */}
          <CloseButton
            data-testid="block-inventory-close"
            aria-label="close"
            onClick={() => {
              void dispatchAction("ui_state.request", { state: UiStateNames.gameScreen });
            }}
          />
        </Group>
        <Body data={data} />
    </Paper>
  );
}
