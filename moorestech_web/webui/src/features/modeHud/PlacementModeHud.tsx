import { Topics, useTopic, type PlacementModeData } from "@/bridge";
import { localizeSelectableTargetName, type SelectableTarget } from "@/shared/placementTarget";
import { L, useI18n } from "@/shared/i18n";
import { tutorialAnchor, TutorialAnchorIds } from "@/shared/tutorialAnchor";
import { FadeRule, GamePanel } from "@/shared/ui";
import styles from "./style.module.css";

export function PlacementModeHud() {
  const data = useTopic(Topics.placementMode);
  const { t } = useI18n();
  if (!data) return null;

  const headingId = "placement-mode-hud-heading";
  const title = t(L.ui.modeHud.placementModeTitle);
  const selected = t(L.ui.modeHud.selectedBlock, { name: localizeSelectableTargetName(selectedTarget(data), t) });

  // 配置情報をクラフト枠で表示する
  // Show placement information in the craft frame
  return (
    <section
      className={styles.placementHud}
      aria-labelledby={headingId}
      data-testid="placement-mode-hud"
      {...tutorialAnchor(TutorialAnchorIds.placementHud)}
    >
      <GamePanel variant="craft">
        <h2 id={headingId} className={styles.label} data-testid="operation-mode-label">{title}</h2>
        <FadeRule />
        <div className={styles.details}>
          <p className={styles.detail} data-testid="operation-mode-detail">{selected}</p>
          <p className={styles.detail} data-testid="operation-mode-detail"><PlacementHeightText height={data.height} /></p>
          {data.unavailableReason.length > 0 && (
            <p className={styles.warning} data-testid="operation-mode-warning">{data.unavailableReason}</p>
          )}
        </div>
      </GamePanel>
    </section>
  );
}

// 削除箱は共用高さだけを表示し、配置対象や配置不能理由を持ち込まない
// Show only the shared height for deletion, without placement targets or placement failure reasons
export function DeleteBoxHeightHud() {
  const data = useTopic(Topics.placementMode);
  if (!data) return null;
  return (
    <div className={styles.placementHud} data-testid="delete-box-height-hud">
      <p className={`${styles.detail} keyHintText`}><PlacementHeightText height={data.height} /></p>
    </div>
  );
}

function PlacementHeightText({ height }: { height: number }) {
  const { t } = useI18n();
  return t(L.ui.modeHud.placementHeight, { height });
}

// wireのidentityを共有の表示名解決用targetへ写す
// Map the wire identity onto the shared display-name resolution target
function selectedTarget(data: PlacementModeData): SelectableTarget {
  switch (data.selectedTargetType) {
    case "block": return { type: "block", guid: data.selectedBlockGuid };
    case "connectTool": return { type: "connectTool", guid: data.selectedConnectToolGuid };
    case "trainCar": return { type: "trainCar", guid: data.selectedTrainCarGuid };
    case "blueprintCopy": return { type: "blueprintCopy" };
    case "raw": return { type: "raw", label: data.selectedName };
  }
}
