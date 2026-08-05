import { useLayoutEffect, useRef, useState } from "react";
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
  // 初期値をストアから復元（lazy初期化子でマウント時1回。前例: shared/treeView/TreeView.tsx）
  // Restore initial values from the session store once per mount via a lazy initializer (precedent: shared/treeView/TreeView.tsx)
  const [stored] = useState(() => loadBuildMenuSessionState());
  const [selectedCategory, setSelectedCategory] = useState<string | null>(stored.categoryGuid);
  const [query, setQuery] = useState(stored.query);
  const [hoveredId, setHoveredId] = useState<string | null>(stored.hoveredEntryId);
  // topic未着の初回renderは早期returnで視口が無いため、視口アタッチ時のcallback refで1回だけ復元する
  // The first render has no viewport (topic not yet arrived → early return), so restore once via a callback ref at viewport attach
  const scrollRestoredRef = useRef(false);
  const scrollViewportRef = useRef<HTMLDivElement | null>(null);
  const attachScrollViewport = (viewport: HTMLDivElement | null) => {
    if (viewport === null) return;
    // 保存先は常に最新の視口を指す。復元だけが初回1回に限られる
    // The save target always points at the newest viewport; only the restore is limited to the first attach
    scrollViewportRef.current = viewport;
    if (scrollRestoredRef.current) return;
    scrollRestoredRef.current = true;
    viewport.scrollTop = loadBuildMenuSessionState().scrollTop;
    // 内容高が足りない復元値はブラウザにクランプされるため、実効値をストアへ揃え直す
    // The browser clamps a restored value taller than the content, so realign the store with the effective one
    updateBuildMenuSessionState({ scrollTop: viewport.scrollTop });
  };
  // scrollイベントは次フレームまで合体されアンマウントに間に合わないため、DOM除去前の実効値を確定保存する
  // Scroll events coalesce until the next frame and miss the unmount, so persist the effective value before DOM removal
  useLayoutEffect(() => () => {
    if (scrollViewportRef.current === null) return;
    updateBuildMenuSessionState({ scrollTop: scrollViewportRef.current.scrollTop });
  }, []);
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
  const hover = (entry: BuildMenuDisplayEntry) => {
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
        {/* GamePanelは内容高で伸びるため、研究パネル前例と同じくバンド高へ明示的に縛る（§8.11の--menu-content-height） */}
        {/* GamePanel grows with its content, so pin it to the band height like the research-panel precedent (§8.11 --menu-content-height) */}
        <GamePanel title={t(L.ui.buildMenu.title)} variant="default" style={{ height: "100%", boxSizing: "border-box" }}>
          <IconButton onClick={close} ariaLabel={t(L.ui.common.close)} className={styles.close} testId="build-menu-close" />
          <div className={styles.columns} data-testid="build-menu-columns">
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
                    onEntryHovered={hover}
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
