import { useTopic, Topics, UiStateNames, dispatchAction } from "@/bridge";
import { GamePanel, PanelCloseButton } from "@/shared/ui";
import { resolveBlockComponent } from "./registry/blockComponentRegistry";
import styles from "./style.module.css";
import BlockItemGrid from "./BlockItemGrid";
import { useI18n } from "@/shared/i18n";
import { tutorialAnchor } from "@/shared/tutorialAnchor";

// ブロック UI のオーバーレイ。uGUI の SubInventoryState 相当で、blockType から中身を静的解決する
// Block UI overlay; the SubInventoryState equivalent, statically resolving the body from blockType
export default function BlockInventoryPanel() {
  const data = useTopic(Topics.blockInventory);
  const { t } = useI18n();
  // 未受信または閉状態なら何も描画しない（ブロック UI が開いていない）
  // Render nothing when not received or closed (no block UI is open)
  if (!data || !data.open) return null;

  // blockType に対応するコンポーネントを解決（未登録は GenericBlockInventory にフォールバック）
  // Resolve the component for blockType (unregistered falls back to GenericBlockInventory)
  const Body = data.source === "block" ? resolveBlockComponent(data.blockType) : null;
  const trainError = data.source === "train" && data.error
    ? t({
        containerMissing: "This train has no item container",
        trainCarMissing: "Train not found",
        openFailed: "Could not open train inventory",
      }[data.error])
    : null;
  const title = data.source === "train" ? t("Train Inventory") : data.blockName;

  return (
    <div className={styles.panel} data-testid="block-inventory">
      <GamePanel variant="default" title={title}>
        {data.source === "train" && trainError && <div data-testid="train-inventory-error">{trainError}</div>}
        {data.source === "train" && !trainError && <BlockItemGrid itemSlots={data.itemSlots} testId="train-inventory-slots" />}
        {data.source === "block" && Body && <Body data={data} />}
      </GamePanel>
      {/* uGUIのEsc/Tab相当のマウス閉じ操作。GameScreenへの遷移をhostへ要求する */}
      {/* Mouse-driven close, like uGUI Esc/Tab; asks the host to transit to GameScreen */}
      <PanelCloseButton
        className={styles.close}
        onClick={() => {
          void dispatchAction("ui_state.request", { state: UiStateNames.gameScreen });
        }}
        ariaLabel={t("Close")}
        testId="block-inventory-close"
        {...tutorialAnchor("inventory.close-button")}
      />
    </div>
  );
}
