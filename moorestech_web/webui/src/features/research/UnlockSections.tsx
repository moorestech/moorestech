import type { ReactNode } from "react";
import type { ResearchNodeData } from "@/bridge";
import { ItemSlot, BlockSlot, FluidSlot } from "@/shared/ui";
import { L, useI18n, type TranslationKey } from "@/shared/i18n";
import { localizeSelectableTargetName } from "@/shared/placementTarget";
import { toUnlockEntries, type UnlockEntry } from "./unlockEntries";
import styles from "./style.module.css";

type Props = { node: ResearchNodeData };

// 見出しと並べ方だけを持つ純データ。描画はrenderUnlockEntryの網羅switchが持つ
// Pure data holding only the heading and the layout; drawing lives in renderUnlockEntry's exhaustive switch
type SectionConfig = {
  // 同じ見出しへまとめる単位。testIdはDOMの目印なのでグルーピングには使わない
  // The grouping unit; testId is a DOM marker and must not double as the group key
  sectionId: string;
  labelKey: TranslationKey;
  testId: string;
  wrapInSlotsRow: boolean;
};

// 種類→セクション設定のルックアップ表。kind追加時はここが欠損しコンパイルエラーになる（D5）
// Kind→section lookup table; adding a kind without covering it here is a compile error (D5)
const SECTIONS: Record<UnlockEntry["kind"], SectionConfig> = {
  block: { sectionId: "blocks", labelKey: L.ui.research.unlockBlocksLabel, testId: "research-unlock-blocks", wrapInSlotsRow: true },
  machineRecipeOutput: { sectionId: "machineRecipes", labelKey: L.ui.research.unlockMachineRecipesLabel, testId: "research-unlock-machine-recipes", wrapInSlotsRow: true },
  itemRecipeView: { sectionId: "items", labelKey: L.ui.research.unlockItemsLabel, testId: "research-unlock-items", wrapInSlotsRow: true },
  rewardItem: { sectionId: "rewardItems", labelKey: L.ui.research.rewardItemsLabel, testId: "research-reward-items", wrapInSlotsRow: true },
  connectTool: { sectionId: "others", labelKey: L.ui.research.unlockOthersLabel, testId: "research-unlock-others", wrapInSlotsRow: false },
  trainCar: { sectionId: "others", labelKey: L.ui.research.unlockOthersLabel, testId: "research-unlock-others", wrapInSlotsRow: false },
};

// 解放物1件の描画。種類を足すとdefaultのnever代入がコンパイルエラーになる
// Draws one unlock entry; adding a kind breaks the never assignment in default at compile time
function renderUnlockEntry(entry: UnlockEntry, t: (key: TranslationKey) => string): ReactNode {
  switch (entry.kind) {
    case "block":
      return (
        <BlockSlot
          key={`ub-${entry.index}-${entry.blockId}-${entry.blockGuid}`}
          blockId={entry.blockId}
          name={localizeSelectableTargetName({ type: "block", guid: entry.blockGuid }, t)}
        />
      );
    case "machineRecipeOutput":
      // D1:出力有無でSlot出し分け
      // D1: renders ItemSlot or FluidSlot by output presence
      if (entry.itemIds.length > 0) {
        return entry.itemIds.map((itemId, i) => <ItemSlot key={`um-${entry.index}-${entry.recipeGuid}-${itemId}-${i}`} itemId={itemId} />);
      }
      return entry.fluids.map((fluid, i) => (
        <FluidSlot key={`umf-${entry.index}-${entry.recipeGuid}-${fluid.fluidGuid}-${i}`}
          fluid={{ fluidId: fluid.fluidId, fluidGuid: fluid.fluidGuid, amount: fluid.amount, capacity: fluid.amount }} />
      ));
    case "itemRecipeView":
      return <ItemSlot key={`uc-${entry.index}-${entry.itemId}`} itemId={entry.itemId} />;
    case "rewardItem":
      return <ItemSlot key={`rw-${entry.index}-${entry.itemId}`} itemId={entry.itemId} count={entry.count} />;
    case "connectTool":
      return <p key={`ot-ct-${entry.index}-${entry.guid}`} className={styles.unlockOtherName}>{localizeSelectableTargetName({ type: "connectTool", guid: entry.guid }, t)}</p>;
    case "trainCar":
      return <p key={`ot-tc-${entry.index}-${entry.guid}`} className={styles.unlockOtherName}>{localizeSelectableTargetName({ type: "trainCar", guid: entry.guid }, t)}</p>;
    default: {
      const exhaustive: never = entry;
      return exhaustive;
    }
  }
}

// 研究解放セクション(D6で2種統合)
// Unlock sections per kind (D6 merges 2 kinds)
export default function UnlockSections({ node }: Props) {
  const { t } = useI18n();
  const entries = toUnlockEntries(node);

  const sections = new Map<string, { labelKey: TranslationKey; testId: string; wrapInSlotsRow: boolean; nodes: ReactNode[] }>();
  for (const entry of entries) {
    const config = SECTIONS[entry.kind];
    const existing = sections.get(config.sectionId);
    const bucket = existing ?? { labelKey: config.labelKey, testId: config.testId, wrapInSlotsRow: config.wrapInSlotsRow, nodes: [] };
    bucket.nodes.push(renderUnlockEntry(entry, t));
    if (!existing) sections.set(config.sectionId, bucket);
  }

  return (
    <>
      {[...sections.values()].map((section) => (
        <div key={section.testId} data-testid={section.testId}>
          <span className={styles.sectionLabel}>{t(section.labelKey)}</span>
          {section.wrapInSlotsRow ? <div className={styles.detailSlots}>{section.nodes}</div> : section.nodes}
        </div>
      ))}
    </>
  );
}
