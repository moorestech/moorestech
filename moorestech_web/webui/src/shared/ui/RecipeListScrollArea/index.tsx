import type { ReactNode } from "react";
import { ScrollArea, Stack } from "@mantine/core";
import styles from "./style.module.css";

type Props = {
  children: ReactNode;
  // 面ごとの追加規則はクラスで渡す
  // Per-panel extra rules arrive as a class
  scrollClassName: string | null;
  listClassName: string;
  listGap: string;
  listTestId: string;
};

// レシピ行のスクローラ。高さは器が決める
// Scroller for recipe rows; the container sets its height
export default function RecipeListScrollArea({ children, scrollClassName, listClassName, listGap, listTestId }: Props) {
  const className = scrollClassName === null ? styles.scroll : `${styles.scroll} ${scrollClassName}`;
  return (
    // 溢れた時だけバーを出す
    // The bar shows only on overflow
    <ScrollArea type="auto" scrollbarSize="var(--recipe-list-scrollbar-reserve)" className={className}>
      <Stack className={listClassName} gap={listGap} data-testid={listTestId}>{children}</Stack>
    </ScrollArea>
  );
}
