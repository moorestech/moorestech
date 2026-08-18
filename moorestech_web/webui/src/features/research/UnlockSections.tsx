import type { ReactNode } from "react";
import type { ResearchNodeData } from "@/bridge";
import { ItemSlot, BlockSlot, FluidSlot } from "@/shared/ui";
import { L, useI18n, type TranslationKey } from "@/shared/i18n";
import { localizeSelectableTargetName } from "@/shared/placementTarget";
import { toUnlockEntries, type UnlockEntry } from "./unlockEntries";
import styles from "./style.module.css";

type Props = { node: ResearchNodeData };

type SectionConfig = {
  labelKey: TranslationKey;
  testId: string;
  // 種類ごとの見た目差分(スロット群 or 名前テキスト行)を吸収する
  // Absorbs the per-kind visual difference (a slot row vs. plain name lines)
  wrapInSlotsRow: boolean;
  render: (entry: UnlockEntry, t: (key: TranslationKey) => string) => ReactNode;
};

// 種類→セクション設定のルックアップ表。kind追加時はここが欠損しコンパイルエラーになる（D5）
// Kind→section lookup table; adding a kind without covering it here is a compile error (D5)
const SECTIONS: Record<UnlockEntry["kind"], SectionConfig> = {
  block: {
    labelKey: L.ui.research.unlockBlocksLabel,
    testId: "research-unlock-blocks",
    wrapInSlotsRow: true,
    render: (entry, t) => {
      if (entry.kind !== "block") return null;
      return (
        <BlockSlot
          key={`ub-${entry.blockId}-${entry.blockGuid}`}
          blockId={entry.blockId}
          name={localizeSelectableTargetName({ type: "block", guid: entry.blockGuid }, t)}
        />
      );
    },
  },
  machineRecipeOutput: {
    labelKey: L.ui.research.unlockMachineRecipesLabel,
    testId: "research-unlock-machine-recipes",
    wrapInSlotsRow: true,
    render: (entry) => {
      if (entry.kind !== "machineRecipeOutput") return null;
      // アイテム出力があればItemSlot、無ければ既存FluidSlotで描く(D1)。液体はcapacity=amountで満杯表示する
      // Item outputs render as ItemSlot; otherwise FluidSlot (D1). Fluids show full via capacity=amount
      if (entry.itemIds.length > 0) {
        return entry.itemIds.map((itemId, i) => <ItemSlot key={`um-${entry.recipeGuid}-${itemId}-${i}`} itemId={itemId} />);
      }
      return entry.fluids.map((fluid, i) => (
        <FluidSlot key={`umf-${entry.recipeGuid}-${fluid.fluidGuid}-${i}`}
          fluid={{ fluidId: fluid.fluidId, fluidGuid: fluid.fluidGuid, amount: fluid.amount, capacity: fluid.amount }} />
      ));
    },
  },
  itemRecipeView: {
    labelKey: L.ui.research.unlockItemsLabel,
    testId: "research-unlock-items",
    wrapInSlotsRow: true,
    render: (entry) => {
      if (entry.kind !== "itemRecipeView") return null;
      return <ItemSlot key={`uc-${entry.itemId}`} itemId={entry.itemId} />;
    },
  },
  rewardItem: {
    labelKey: L.ui.research.rewardItemsLabel,
    testId: "research-reward-items",
    wrapInSlotsRow: true,
    render: (entry) => {
      if (entry.kind !== "rewardItem") return null;
      return <ItemSlot key={`rw-${entry.itemId}`} itemId={entry.itemId} count={entry.count} />;
    },
  },
  connectTool: {
    labelKey: L.ui.research.unlockOthersLabel,
    testId: "research-unlock-others",
    wrapInSlotsRow: false,
    render: (entry, t) => {
      if (entry.kind !== "connectTool") return null;
      return <p key={`ot-ct-${entry.guid}`} className={styles.unlockOtherName}>{localizeSelectableTargetName({ type: "connectTool", guid: entry.guid }, t)}</p>;
    },
  },
  trainCar: {
    labelKey: L.ui.research.unlockOthersLabel,
    testId: "research-unlock-others",
    wrapInSlotsRow: false,
    render: (entry, t) => {
      if (entry.kind !== "trainCar") return null;
      return <p key={`ot-tc-${entry.guid}`} className={styles.unlockOtherName}>{localizeSelectableTargetName({ type: "trainCar", guid: entry.guid }, t)}</p>;
    },
  },
};

// 種類別ラベル付きセクション（ADR 0014）。connectTool/trainCarは同じtestId/labelKeyで1セクションへ束ねる（D6）
// Labeled unlock sections per kind (ADR 0014); connectTool/trainCar share one testId/labelKey (D6)
export default function UnlockSections({ node }: Props) {
  const { t } = useI18n();
  const entries = toUnlockEntries(node);

  const sections = new Map<string, { labelKey: TranslationKey; testId: string; wrapInSlotsRow: boolean; nodes: ReactNode[] }>();
  for (const entry of entries) {
    const config = SECTIONS[entry.kind];
    const existing = sections.get(config.testId);
    const bucket = existing ?? { labelKey: config.labelKey, testId: config.testId, wrapInSlotsRow: config.wrapInSlotsRow, nodes: [] };
    bucket.nodes.push(config.render(entry, t));
    if (!existing) sections.set(config.testId, bucket);
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
