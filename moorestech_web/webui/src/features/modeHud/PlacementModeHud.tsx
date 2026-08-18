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
  const height = t(L.ui.modeHud.placementHeight, { height: data.height });

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
          <p className={styles.detail} data-testid="operation-mode-detail">{height}</p>
          {data.unavailableReason.length > 0 && (
            <p className={styles.warning} data-testid="operation-mode-warning">{data.unavailableReason}</p>
          )}
        </div>
      </GamePanel>
    </section>
  );
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
