import { useTopic, Topics, UiStateNames, dispatchAction } from "@/bridge";
import { GamePanel, PanelCloseButton } from "@/shared/ui";
import { resolveBlockComponent } from "./registry/blockComponentRegistry";
import styles from "./style.module.css";
import BlockItemGrid from "./BlockItemGrid";
import { buildMachineRecipeSelectionRows } from "./details/machine/machineRecipeSelectionLogic";
import { useI18n } from "@/shared/i18n";
import { tutorialAnchor, TutorialAnchorIds } from "@/shared/tutorialAnchor";

// ブロック UI のオーバーレイ。uGUI の SubInventoryState 相当で、blockType から中身を静的解決する
// Block UI overlay; the SubInventoryState equivalent, statically resolving the body from blockType
export default function BlockInventoryPanel() {
  const data = useTopic(Topics.blockInventory);
  const machineRecipes = useTopic(Topics.machineRecipes);
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

  // レシピ選択を持つ機械だけ研究パネル同等のviewer〜items占有大型パネルへ広げる
  // Only recipe-capable machines expand to the research-sized panel spanning viewer..items
  const isLargeMachinePanel = data.source === "block" && data.machine !== undefined
    && buildMachineRecipeSelectionRows(machineRecipes?.recipes ?? [], data.machine.blockGuid, data.machine.selectedRecipeGuid).length > 0;

  return (
    <div
      className={isLargeMachinePanel ? `${styles.panel} ${styles.panelLarge}` : styles.panel}
      data-testid="block-inventory"
      data-large={isLargeMachinePanel ? "true" : undefined}
    >
      <GamePanel
        variant="default"
        title={title}
        style={{
          paddingBottom: "var(--block-panel-bottom-safe-area)",
          // 内容量幅の小型パネルは右余白が10pxしかなくフェード帯で途切れて見えるため、左と対称の右余白を足す
          // Content-sized small panels have only a 10px right padding that dies in the fade band, so mirror the left inset
          ...(isLargeMachinePanel
            ? { height: "100%", boxSizing: "border-box" }
            : { paddingRight: "var(--block-panel-right-safe-area)" }),
        }}
      >
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
        {...tutorialAnchor(TutorialAnchorIds.inventoryCloseButton)}
      />
    </div>
  );
}
