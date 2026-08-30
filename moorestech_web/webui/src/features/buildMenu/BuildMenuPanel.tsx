import { useState } from "react";
import { ScrollArea } from "@mantine/core";
import { useTopic, dispatchAction, Topics, UiStateNames } from "@/bridge";
import { GamePanel, IconButton } from "@/shared/ui";
import { L, useI18n } from "@/shared/i18n";
import {
  groupBuildMenuCategories,
  localizeBuildMenuEntries,
  searchBuildMenuEntries,
  type BuildMenuDisplayEntry,
} from "./logic/buildMenuGrouping";
import { useBuildMenuCategoryScroll } from "./hooks/useBuildMenuCategoryScroll";
import { BuildMenuCategoryList } from "./views/BuildMenuCategoryList";
import { BuildMenuDetailSidebar } from "./views/BuildMenuDetailSidebar";
import { BuildMenuSearchInput } from "./views/BuildMenuSearchInput";
import { CategorySidebar } from "./views/CategorySidebar";
import { loadBuildMenuSessionState, updateBuildMenuSessionState } from "./sessionState/buildMenuSessionState";
import styles from "./style.module.css";

// web版3カラム、中央は1本スクロール(ADR0045)
// Web 3-column layout; center is one scroll (ADR 0045)
export function BuildMenuPanel() {
  const { t } = useI18n();
  const data = useTopic(Topics.buildMenu);
  // ストアから初期値を復元
  // Restore initial values from the session store
  const [stored] = useState(() => loadBuildMenuSessionState());
  const [query, setQuery] = useState(stored.query);
  const [hoveredId, setHoveredId] = useState<string | null>(stored.hoveredEntryId);

  // 表示名を一度解決し全表示へ共有。サイドバーは絞り込み前、リストは絞り込み後の群を見る
  // Resolve display names once; the sidebar sees unfiltered groups, the list sees filtered ones
  const displayEntries = data ? localizeBuildMenuEntries(data.entries, t) : [];
  const searching = query !== "";
  // 「中身のあるカテゴリ」の判定を群化1本へ寄せる。別基準を持つとサイドバーに恒久disabledのカテゴリが出る
  // Derives "categories with content" from the one grouping; a second criterion leaves permanently disabled categories in the sidebar
  const allGroups = data ? groupBuildMenuCategories(data.categories, displayEntries) : [];
  // 非検索時はsearchBuildMenuEntries("")が入力をそのまま返すため、絞り込み計算自体を省く
  // searchBuildMenuEntries("") returns the input unchanged, so skip the filtering step entirely outside search
  const shownGroups = data && searching
    ? groupBuildMenuCategories(data.categories, searchBuildMenuEntries(query, displayEntries))
    : allGroups;
  const shownGuids = shownGroups.map((group) => group.categoryGuid);
  const scroll = useBuildMenuCategoryScroll(shownGuids);
  if (!data) return null;

  const sidebarItems = allGroups.map((group) => ({
    categoryGuid: group.categoryGuid,
    disabled: !shownGuids.includes(group.categoryGuid),
  }));

  // sticky:離脱で消さず引き直す
  // Sticky: never clear; re-resolve on rebroadcast
  const detailEntry = hoveredId
    ? displayEntries.find((entry) => entry.id === hoveredId) ?? null
    : null;
  const hover = (entry: BuildMenuDisplayEntry) => {
    setHoveredId(entry.id);
    updateBuildMenuSessionState({ hoveredEntryId: entry.id });
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
        {/* GamePanelは内容高で伸びるため、blockInventory前例と同じくバンド高へ明示的に縛る（§8.11の--menu-content-height） */}
        {/* GamePanel grows with its content, so pin it to the band height like the blockInventory precedent (§8.11 --menu-content-height) */}
        <GamePanel title={t(L.ui.buildMenu.title)} variant="default" style={{ height: "100%", boxSizing: "border-box" }}>
          <IconButton onClick={close} ariaLabel={t(L.ui.common.close)} className={styles.close} testId="build-menu-close" />
          <div className={styles.columns} data-testid="build-menu-columns">
            <div className={styles.sidebar}>
              <CategorySidebar
                categories={sidebarItems}
                selected={scroll.activeCategoryGuid}
                onSelect={scroll.jumpTo}
              />
            </div>
            <div className={styles.main}>
              <BuildMenuSearchInput value={query} onChange={changeQuery} />
              <ScrollArea
                className={styles.scroll}
                type="auto"
                viewportRef={scroll.attachViewport}
                onScrollPositionChange={({ y }) => scroll.handleScroll(y)}
              >
                {shownGroups.length === 0 && searching ? (
                  <span className={styles.noHit}>{t(L.ui.buildMenu.noResults)}</span>
                ) : (
                  <BuildMenuCategoryList
                    groups={shownGroups}
                    spacerHeight={scroll.spacerHeight}
                    headingRef={scroll.headingRef}
                    attachGroup={scroll.attachGroup}
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
