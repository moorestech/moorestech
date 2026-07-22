import { FadeRule, ItemSlot } from "@/shared/ui";
import { useI18n } from "@/shared/i18n";
import type { BuildMenuEntryData } from "@/bridge";
import styles from "./style.module.css";

type Props = { entry: BuildMenuEntryData | null };

// §8.7の固定高プレビュー。ホバー中エントリを表示し、無ければ案内テキスト
// §8.7 fixed-height preview: shows the hovered entry, otherwise a hint
export function BuildMenuDetailPreview({ entry }: Props) {
  const { t } = useI18n();
  return (
    <div className={styles.preview} data-testid="build-menu-preview">
      <div className={styles.previewBody}>
        {entry === null ? (
          <span className={styles.previewHint}>{t("カーソルを合わせると詳細を表示します")}</span>
        ) : (
          <>
            {entry.iconUrl && <img className={styles.previewIcon} src={entry.iconUrl} alt={entry.label} draggable={false} />}
            <span className={styles.previewName}>{entry.label}</span>
            {entry.requiredItems.length > 0 && (
              <div className={styles.previewCost}>
                {entry.requiredItems.map((item) => (
                  <ItemSlot key={item.itemId} itemId={item.itemId} count={item.count} />
                ))}
              </div>
            )}
          </>
        )}
      </div>
      <FadeRule />
    </div>
  );
}
