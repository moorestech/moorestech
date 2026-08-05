import { useRef, useState } from "react";
import { ScrollArea } from "@mantine/core";
import { useTopic, dispatchAction, Topics, UiStateNames } from "@/bridge";
import { GamePanel, IconButton } from "@/shared/ui";
import { L, useI18n } from "@/shared/i18n";
import {
  localizeBuildMenuEntries,
  resolveSelectedCategory,
  searchSections,
  sectionsForCategory,
  visibleCategories,
  type BuildMenuDisplayEntry,
} from "./buildMenuGrouping";
import { BuildMenuCategoryGrid } from "./BuildMenuCategoryGrid";
import { BuildMenuDetailSidebar } from "./BuildMenuDetailSidebar";
import { BuildMenuSearchInput } from "./BuildMenuSearchInput";
import { CategorySidebar } from "./CategorySidebar";
import { loadBuildMenuSessionState, updateBuildMenuSessionState } from "./sessionState/buildMenuSessionState";
import styles from "./style.module.css";

// uGUI BuildMenuView の web 版。stage水平中央の3カラム（§8.11・ADR-0007）
// Web version of uGUI BuildMenuView: three columns centered horizontally on the stage (§8.11, ADR-0007)
export function BuildMenuPanel() {
  const { t } = useI18n();
  const data = useTopic(Topics.buildMenu);
  // 初期値をストアから復元（マウント時1回）
  // Restore initial values from the session store (once per mount)
  const stored = loadBuildMenuSessionState();
  const [selectedCategory, setSelectedCategory] = useState<string | null>(stored.categoryGuid);
  const [query, setQuery] = useState(stored.query);
  const [hoveredId, setHoveredId] = useState<string | null>(stored.hoveredEntryId);
  // topic未着の初回renderは早期returnで視口が無いため、視口アタッチ時のcallback refで1回だけ復元する
  // The first render has no viewport (topic not yet arrived → early return), so restore once via a callback ref at viewport attach
  const scrollRestoredRef = useRef(false);
  const attachScrollViewport = (viewport: HTMLDivElement | null) => {
    if (viewport === null || scrollRestoredRef.current) return;
    scrollRestoredRef.current = true;
    viewport.scrollTop = loadBuildMenuSessionState().scrollTop;
  };
  if (!data) return null;

  // 表示名を一度解決し全表示へ共有
  // Resolve display names once and share them across views
  const displayEntries = localizeBuildMenuEntries(data.entries, t);
  const visible = visibleCategories(data.categories, displayEntries);
  const searching = query !== "";
  const currentCategory = resolveSelectedCategory(selectedCategory, visible);
  const sections = searching
    ? searchSections(query, data.categories, displayEntries)
    : currentCategory !== null
      ? sectionsForCategory(currentCategory, data.categories, displayEntries)
      : [];

  // stickyのため離脱では消さない。topic再配信で消えたエントリは現データ側へ引き直す
  // Sticky: never clear on leave; if a rebroadcast removed the entry, re-resolve against live data
  const detailEntry = hoveredId
    ? displayEntries.find((entry) => entry.id === hoveredId) ?? null
    : null;
  const hover = (entry: BuildMenuDisplayEntry | null) => {
    if (entry === null) return;
    setHoveredId(entry.id);
    updateBuildMenuSessionState({ hoveredEntryId: entry.id });
  };

  // 変更時にプッシュ保存（§設計原則: 毎tick比較でなく変化点で保存）
  // Push-save on change (per design principles: save at the change point, not by per-frame comparison)
  const selectCategory = (categoryGuid: string) => {
    setSelectedCategory(categoryGuid);
    updateBuildMenuSessionState({ categoryGuid });
  };
  const changeQuery = (next: string) => {
    setQuery(next);
    updateBuildMenuSessionState({ query: next });
  };

  const select = (entry: BuildMenuDisplayEntry) =>
    void dispatchAction("build_menu.select", { id: entry.id });
  // BPのGuidを設置対象と削除対象の共通identityとして使う
  // Use the blueprint GUID as the shared identity for placement and deletion
  const remove = (entry: BuildMenuDisplayEntry) =>
    void dispatchAction("blueprint.delete", { id: entry.id });
  // 閉じるはGameScreen遷移要求
  // Close requests a GameScreen transition
  const close = () => void dispatchAction("ui_state.request", { state: UiStateNames.gameScreen });

  return (
    <div className={styles.panelBand}>
      <div className={styles.panel} data-testid="build-menu-panel">
        <GamePanel title={t(L.ui.buildMenu.title)} variant="default">
          <IconButton onClick={close} ariaLabel={t(L.ui.common.close)} className={styles.close} testId="build-menu-close" />
          <div className={styles.columns}>
            <div className={styles.sidebar}>
              <CategorySidebar
                categories={visible}
                selected={currentCategory ?? ""}
                disabled={searching}
                onSelect={selectCategory}
              />
            </div>
            <div className={styles.main}>
              <BuildMenuSearchInput value={query} onChange={changeQuery} />
              <ScrollArea
                className={styles.scroll}
                type="auto"
                viewportRef={attachScrollViewport}
                onScrollPositionChange={({ y }) => updateBuildMenuSessionState({ scrollTop: y })}
              >
                {sections.length === 0 && searching ? (
                  <span className={styles.noHit}>{t(L.ui.buildMenu.noResults)}</span>
                ) : (
                  <BuildMenuCategoryGrid
                    sections={sections}
                    compositeHeading={searching}
                    onSelect={select}
                    onDelete={remove}
                    onHoverChange={hover}
                  />
                )}
              </ScrollArea>
            </div>
            <BuildMenuDetailSidebar entry={detailEntry} />
          </div>
        </GamePanel>
      </div>
    </div>
  );
}
