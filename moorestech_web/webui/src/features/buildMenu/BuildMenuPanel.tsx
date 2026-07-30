import { useState } from "react";
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
import { BuildMenuDetailPreview } from "./BuildMenuDetailPreview";
import { BuildMenuSearchInput } from "./BuildMenuSearchInput";
import { CategorySidebar } from "./CategorySidebar";
import styles from "./style.module.css";

// uGUI BuildMenuView の web 版。カテゴリ導出は純関数、選択は build_menu.select で Unity へ届く
// Web version of uGUI BuildMenuView; categories via pure functions, selections reach Unity via build_menu.select
export function BuildMenuPanel() {
  const { t } = useI18n();
  const data = useTopic(Topics.buildMenu);
  const [selectedCategory, setSelectedCategory] = useState<string | null>(null);
  const [query, setQuery] = useState("");
  const [hovered, setHovered] = useState<BuildMenuDisplayEntry | null>(null);
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

  // topic再配信で hovered が消えたエントリを指し続けても、描画時に現データ側の実在エントリへ引き直す
  // If a topic rebroadcast leaves hovered pointing at a removed entry, re-resolve to the live entry at render time
  const previewEntry = hovered
    ? displayEntries.find((entry) => entry.entryType === hovered.entryType && entry.entryKey === hovered.entryKey) ?? null
    : null;

  const select = (entry: BuildMenuDisplayEntry) =>
    void dispatchAction("build_menu.select", { entryType: entry.entryType, entryKey: entry.entryKey });
  const remove = (entry: BuildMenuDisplayEntry) => void dispatchAction("blueprint.delete", { name: entry.entryKey });
  // 閉じるはGameScreen遷移要求
  // Close requests a GameScreen transition
  const close = () => void dispatchAction("ui_state.request", { state: UiStateNames.gameScreen });

  return (
    <div className={styles.panel} data-testid="build-menu-panel">
      <GamePanel title={t(L.ui.buildMenu.title)} variant="default">
        <IconButton onClick={close} ariaLabel={t(L.ui.common.close)} className={styles.close} testId="build-menu-close" />
        <div className={styles.columns}>
          <CategorySidebar
            categories={visible}
            selected={currentCategory ?? ""}
            disabled={searching}
            onSelect={setSelectedCategory}
          />
          <div className={styles.main}>
            <BuildMenuSearchInput value={query} onChange={setQuery} />
            <BuildMenuDetailPreview entry={previewEntry} />
            <ScrollArea className={styles.scroll} type="auto">
              {sections.length === 0 && searching ? (
                <span className={styles.noHit}>{t(L.ui.buildMenu.noResults)}</span>
              ) : (
                <BuildMenuCategoryGrid
                  sections={sections}
                  compositeHeading={searching}
                  onSelect={select}
                  onDelete={remove}
                  onHoverChange={setHovered}
                />
              )}
            </ScrollArea>
          </div>
        </div>
      </GamePanel>
    </div>
  );
}
