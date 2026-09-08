import type { ReactNode } from "react";
import { ScrollArea, Stack } from "@mantine/core";
import styles from "./style.module.css";

type Props = {
  children: ReactNode;
  // 面ごとの追加規則はクラスで渡す。無い面は省略する
  // Per-panel extra rules arrive as a class; panels without any omit it
  scrollClassName?: string;
  listClassName?: string;
  listGap: string;
  listTestId: string;
};

// レシピ行のスクローラ。高さは器が決め、行の逃げ余白はここが持つ
// Scroller for recipe rows; the container sets its height and this owns the rows' bleed padding
export default function RecipeListScrollArea({ children, scrollClassName, listClassName, listGap, listTestId }: Props) {
  const scrollClasses = scrollClassName === undefined ? styles.scroll : `${styles.scroll} ${scrollClassName}`;
  const listClasses = listClassName === undefined ? styles.list : `${styles.list} ${listClassName}`;
  return (
    // 溢れた時だけバーを出す
    // The bar shows only on overflow
    <ScrollArea type="auto" scrollbarSize="var(--recipe-list-scrollbar-reserve)" className={scrollClasses}>
      <Stack className={listClasses} gap={listGap} data-testid={listTestId}>{children}</Stack>
    </ScrollArea>
  );
}
