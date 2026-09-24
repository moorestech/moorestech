import { Topics, useTopic } from "@/bridge";
import { LanguageSelect } from "@/features/settings";
import { L, useI18n } from "@/shared/i18n";
import { tutorialAnchor, TutorialAnchorIds } from "@/shared/tutorialAnchor";
import { BugReportForm } from "./BugReportForm";
import { PauseMenuSubPage } from "./PauseMenuSubPage";
import { PauseMenuTopPage } from "./PauseMenuTopPage";
import { useBugReportDraft } from "./useBugReportDraft";
import styles from "./style.module.css";

// どの画面を出すかはC#が配るpageだけで決める。書きかけはここで持ち、画面を行き来しても残す
// Which page shows is decided only by the page C# publishes; the draft lives here so it survives page moves
export function PauseMenuPanel() {
  const data = useTopic(Topics.pauseMenu);
  const { locale, t } = useI18n();
  const draft = useBugReportDraft();

  return (
    <section className={styles.panel} data-testid="pause-menu" {...tutorialAnchor(TutorialAnchorIds.pauseMenu)}>
      <div data-testid={`pause-menu-locale-${locale}`}>{renderPage()}</div>
    </section>
  );

  function renderPage() {
    // 初回配信前はトップの枠のみ。確保状態待ち
    // Before first delivery, only top renders; waits for capture status
    if (!data) return <PauseMenuTopPage disconnected={false} />;
    switch (data.page) {
      case "top": return <PauseMenuTopPage disconnected={data.disconnected} />;
      case "settings": return <PauseMenuSubPage title={t(L.ui.pauseMenu.settings)}><LanguageSelect /></PauseMenuSubPage>;
      case "bugReport": return <PauseMenuSubPage title={t(L.ui.pauseMenu.bugReport)}><BugReportForm status={data.bugReport} draft={draft} /></PauseMenuSubPage>;
      default:
        console.warn(`[PauseMenuPanel] Unknown pause menu page: ${String(data.page)}. Rendering the top page.`);
        return <PauseMenuTopPage disconnected={data.disconnected} />;
    }
  }
}
