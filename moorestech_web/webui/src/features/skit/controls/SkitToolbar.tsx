import { dispatchAction, type ActionPayloads, type SkitIntent } from "@/bridge";
import { L, useI18n } from "@/shared/i18n";
import { IconButton } from "@/shared/ui";
import { AutoIcon, HideUiIcon, ShowUiIcon, SkipIcon } from "../icons";
import styles from "./SkitToolbar.module.css";

type Props = {
  base: ActionPayloads["skit.advance"];
  allowedIntents: ReadonlySet<SkitIntent>;
  autoEnabled: boolean;
  skipActive: boolean;
};

// 画面右上に並ぶ面を持たないアイコンボタン。UnityのLogは本体未配線のためWebでは出さない
// Faceless icon buttons in the screen's top-right; Unity's Log stays out because its backend is unwired
export function SkitToolbar({ base, allowedIntents, autoEnabled, skipActive }: Props) {
  const { t } = useI18n();

  return (
    <div className={styles.toolbar}>
      {/* 自動送りのon/offは同一SVGの色替えで示し、アイコン自体は差し替えない */}
      {/* Auto-advance on/off is a color swap on one SVG; the icon itself is never replaced */}
      <IconButton className={styles.toolButton} ariaLabel={t(L.ui.skit.auto)}
        aria-pressed={autoEnabled} data-enabled={autoEnabled} disabled={!allowedIntents.has("set-auto")}
        onClick={() => void dispatchAction("skit.set_auto", { ...base, enabled: !autoEnabled })}>
        <AutoIcon />
      </IconButton>
      <IconButton className={styles.toolButton} ariaLabel={t(L.ui.skit.skip)}
        disabled={!allowedIntents.has("skip") || skipActive}
        onClick={() => void dispatchAction("skit.skip", base)}>
        <SkipIcon />
      </IconButton>
      <IconButton className={styles.toolButton} ariaLabel={t(L.ui.skit.hideUi)}
        disabled={!allowedIntents.has("set-ui-hidden")}
        onClick={() => void dispatchAction("skit.set_ui_hidden", { ...base, hidden: true })}>
        <HideUiIcon />
      </IconButton>
    </div>
  );
}

// CEFはスキット中にキー入力主権を持たないため、Escape相当の復帰をWeb専用ボタンで供給する
// CEF holds no key-input authority during skits, so this Web-only button supplies the Escape-equivalent restore
export function SkitRestoreButton({ base }: { base: ActionPayloads["skit.advance"] }) {
  const { t } = useI18n();

  return (
    <div className={styles.toolbar}>
      <IconButton className={styles.toolButton} ariaLabel={t(L.ui.skit.showUi)} testId="skit-show-ui"
        onClick={() => void dispatchAction("skit.set_ui_hidden", { ...base, hidden: false })}>
        <ShowUiIcon />
      </IconButton>
    </div>
  );
}
