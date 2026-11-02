import { L, useI18n, useItemDisplayName } from "@/shared/i18n";
import styles from "../style.module.css";

type Props = { itemId: number; level: number };

// 製作上限の解放行。アイテム名の解決にフックが要るので部品へ切り出す（UnlockFluidLabel と同じ形）
// The craft-cap unlock row; resolving the item name needs a hook, so it is its own component (same shape as UnlockFluidLabel)
export default function UnlockCraftCapLabel({ itemId, level }: Props) {
  const { t } = useI18n();
  const itemDisplayName = useItemDisplayName();
  return (
    <p className={styles.unlockOtherName} data-testid={`research-unlock-craft-cap-${itemId}`}>
      {t(L.ui.research.unlockLevelInheritanceCraftCap, { itemName: itemDisplayName(itemId), level })}
    </p>
  );
}
