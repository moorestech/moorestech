import type { MachineRecipeLevelInheritance } from "@/bridge";
import { L, type InterpolationValues, type TranslationKey } from "@/shared/i18n";

type LevelInheritanceBadgeText = { key: TranslationKey; values: InterpolationValues };

// 継承レシピのバッジ文言（キーと差し込み値）。継承しないレシピは null
// Badge copy (key and values) for an inheritance recipe; null for a non-inheritance recipe
export function levelInheritanceBadgeText(inheritance: MachineRecipeLevelInheritance): LevelInheritanceBadgeText | null {
  switch (inheritance.kind) {
    case "none":
      return null;
    case "inherit":
      return { key: L.ui.recipe.levelInheritance, values: {} };
    case "inheritUpToUnlocked":
      return { key: L.ui.recipe.levelInheritanceCapped, values: { level: inheritance.maxCraftableLevel } };
  }
}
