import { dispatchAction, type ActionPayloads } from "@/bridge";
import { useI18n } from "@/shared/i18n";
import { AutoIcon, HideUiIcon, ShowUiIcon, SkipIcon } from "../icons";
import styles from "./toolbar.module.css";

type Props = {
  base: ActionPayloads["skit.advance"];
  allowedIntents: ReadonlySet<string>;
  autoEnabled: boolean;
  skipActive: boolean;
};

// 画面右上に並ぶ面を持たないアイコンボタン。UnityのLogは本体未配線のためWebでは出さない
// Faceless icon buttons in the screen's top-right; Unity's Log stays out because its backend is unwired
export function SkitToolbar({ base, allowedIntents, autoEnabled, skipActive }: Props) {
  const { t } = useI18n();

  return (
    <div className={styles.toolbar}
      onClick={(event) => event.stopPropagation()} onKeyDown={(event) => event.stopPropagation()}>
      {/* 自動送りのon/offは同一SVGの色替えで示し、アイコン自体は差し替えない */}
      {/* Auto-advance on/off is a color swap on one SVG; the icon itself is never replaced */}
      <button className={styles.toolButton} type="button" aria-label={t("Auto")}
        aria-pressed={autoEnabled} data-enabled={autoEnabled} disabled={!allowedIntents.has("set-auto")}
        onClick={() => void dispatchAction("skit.set_auto", { ...base, enabled: !autoEnabled })}>
        <AutoIcon />
      </button>
      <button className={styles.toolButton} type="button" aria-label={t("Skip")}
        disabled={!allowedIntents.has("skip") || skipActive}
        onClick={() => void dispatchAction("skit.skip", base)}>
        <SkipIcon />
      </button>
      <button className={styles.toolButton} type="button" aria-label={t("Hide UI")}
        disabled={!allowedIntents.has("set-ui-hidden")}
        onClick={() => void dispatchAction("skit.set_ui_hidden", { ...base, hidden: true })}>
        <HideUiIcon />
      </button>
    </div>
  );
}

// CEFはスキット中にキー入力主権を持たないため、Escape相当の復帰をWeb専用ボタンで供給する
// CEF holds no key-input authority during skits, so this Web-only button supplies the Escape-equivalent restore
export function SkitRestoreButton({ base }: { base: ActionPayloads["skit.advance"] }) {
  const { t } = useI18n();

  return (
    <div className={styles.toolbar}>
      <button className={styles.toolButton} type="button" aria-label={t("Show UI")} data-testid="skit-show-ui"
        onClick={() => void dispatchAction("skit.set_ui_hidden", { ...base, hidden: false })}>
        <ShowUiIcon />
      </button>
    </div>
  );
}
