import type { ResearchNodeData } from "@/bridge";
import { ItemSlot, BlockSlot } from "@/shared/ui";
import {
  L,
  blockNameKey,
  connectToolNameKey,
  trainCarNameKey,
  useI18n,
} from "@/shared/i18n";
import styles from "./style.module.css";

type Props = { node: ResearchNodeData };

// 解放物の種類別ラベル付きセクション（ADR 0014）。空の種類は出さない
// Labeled unlock sections per kind (ADR 0014); empty kinds render nothing
export default function UnlockSections({ node }: Props) {
  const { t } = useI18n();
  const otherNames = [
    ...node.unlockConnectToolGuids.map((guid) => t(connectToolNameKey(guid))),
    ...node.unlockTrainCarGuids.map((guid) => t(trainCarNameKey(guid))),
  ];
  return (
    <>
      {node.unlockBlocks.length > 0 && (
        <div data-testid="research-unlock-blocks">
          <span className={styles.sectionLabel}>{t(L.ui.research.unlockBlocksLabel)}</span>
          <div className={styles.detailSlots}>
            {node.unlockBlocks.map((b, i) => (
              <BlockSlot key={`ub-${b.blockId}-${i}`} blockId={b.blockId} name={t(blockNameKey(b.blockGuid))} />
            ))}
          </div>
        </div>
      )}
      {node.unlockMachineRecipeOutputItemIds.length > 0 && (
        <div data-testid="research-unlock-machine-recipes">
          <span className={styles.sectionLabel}>{t(L.ui.research.unlockMachineRecipesLabel)}</span>
          <div className={styles.detailSlots}>
            {node.unlockMachineRecipeOutputItemIds.map((id, i) => <ItemSlot key={`um-${id}-${i}`} itemId={id} />)}
          </div>
        </div>
      )}
      {node.unlockItemIds.length > 0 && (
        <div data-testid="research-unlock-craft-recipes">
          <span className={styles.sectionLabel}>{t(L.ui.research.unlockCraftRecipesLabel)}</span>
          <div className={styles.detailSlots}>
            {node.unlockItemIds.map((id, i) => <ItemSlot key={`uc-${id}-${i}`} itemId={id} />)}
          </div>
        </div>
      )}
      {node.rewardItems.length > 0 && (
        <div data-testid="research-reward-items">
          <span className={styles.sectionLabel}>{t(L.ui.research.rewardItemsLabel)}</span>
          <div className={styles.detailSlots}>
            {node.rewardItems.map((r, i) => <ItemSlot key={`rw-${r.itemId}-${i}`} itemId={r.itemId} count={r.count} />)}
          </div>
        </div>
      )}
      {otherNames.length > 0 && (
        <div data-testid="research-unlock-others">
          <span className={styles.sectionLabel}>{t(L.ui.research.unlockOthersLabel)}</span>
          {otherNames.map((name, i) => <p key={`ot-${i}`} className={styles.unlockOtherName}>{name}</p>)}
        </div>
      )}
    </>
  );
}
