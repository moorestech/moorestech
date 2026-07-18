import { defineConfig } from "eslint/config";
import tseslint from "typescript-eslint";
import { noJsxVisibleLiteral } from "./eslint-rules/no-jsx-visible-literal.js";

// Phase Dまで未変換の既存画面コンポーネントだけをファイル単位で固定する
// Freeze only existing unconverted screen components by file until Phase D
const legacyUnlocalizedFiles = [
  "src/app/App.tsx",
  "src/app/AppErrorBoundary.tsx",
  "src/features/blockInventory/BlockInventoryKeyHandler.tsx",
  "src/features/blockInventory/views/ElectricToGearInventory.tsx",
  "src/features/blockInventory/BlockInventoryPanel.tsx",
  "src/features/blockInventory/BlockItemGrid.tsx",
  "src/features/blockInventory/details/GearSection.tsx",
  "src/features/blockInventory/details/GeneratorSection.tsx",
  "src/features/blockInventory/details/LackHighlightText/index.tsx",
  "src/features/blockInventory/details/MachineSection.tsx",
  "src/features/blockInventory/details/MinerSection.tsx",
  "src/features/blockInventory/details/NetworkSections.tsx",
  "src/features/blockInventory/details/PowerRateText.tsx",
  "src/features/blockInventory/views/FilterSplitterInventory.tsx",
  "src/features/blockInventory/views/SectionStackView.tsx",
  "src/features/buildMenu/BuildMenuPanel.tsx",
  "src/features/buildMenu/BuildMenuSlot.tsx",
  "src/features/inventory/DebugActionButton.tsx",
  "src/features/inventory/HotbarPanel/index.tsx",
  "src/features/inventory/InventoryPanel/GrabOverlay.tsx",
  "src/features/inventory/InventoryPanel/index.tsx",
  "src/features/inventory/InventoryScreenChrome.tsx",
  "src/features/modal/ModalHost.tsx",
  "src/features/progress/ProgressBar.tsx",
  "src/features/recipe/panels/ItemListPanel.tsx",
  "src/features/recipe/panels/RecipeViewer.tsx",
  "src/features/recipe/selection/RecipeSelectionKeyHandler.tsx",
  "src/features/recipe/views/CraftProgressArrow.tsx",
  "src/features/recipe/views/CraftRecipeView.tsx",
  "src/features/recipe/views/ItemHeader.tsx",
  "src/features/recipe/views/MachineRecipeView.tsx",
  "src/features/recipe/views/RecipeContent.tsx",
  "src/features/recipe/views/RecipePager.tsx",
  "src/features/research/ResearchNodeCard.tsx",
  "src/features/research/ResearchTreePanel.tsx",
  "src/features/toast/ToastHost.tsx",
  "src/shared/ui/BlockIcon.tsx",
  "src/shared/ui/BlockSlot/index.tsx",
  "src/shared/ui/ConnectingPlaceholder/index.tsx",
  "src/shared/ui/FluidSlot/index.tsx",
  "src/shared/ui/FluidSlotRow/index.tsx",
  "src/shared/ui/GameIcon/index.tsx",
  "src/shared/ui/GamePanel/index.tsx",
  "src/shared/ui/ItemIcon.tsx",
  "src/shared/ui/ItemSlot/index.tsx",
  "src/shared/ui/ProgressArrow/index.tsx",
  "src/shared/ui/SlotFrame/index.tsx",
  "src/shared/ui/SlotGrid/index.tsx",
];

export default defineConfig([
  {
    files: ["src/**/*.{ts,tsx}"],
    ignores: ["src/bridge/**"],
    languageOptions: {
      parser: tseslint.parser,
      parserOptions: {
        ecmaFeatures: { jsx: true },
        sourceType: "module",
      },
    },
    rules: {
      "no-restricted-imports": [
        "error",
        {
          patterns: [
            {
              group: [
                "@/bridge/*",
                "@/features/toast/toastStore",
                "@/shared/ui/SlotGrid",
                "@/shared/ui/*/style.module.css",
              ],
              message: "Use the package barrel instead of a deep import.",
            },
          ],
        },
      ],
    },
  },
  {
    files: ["src/**/*.tsx"],
    ignores: legacyUnlocalizedFiles,
    plugins: {
      local: {
        rules: { "no-jsx-visible-literal": noJsxVisibleLiteral },
      },
    },
    rules: { "local/no-jsx-visible-literal": "error" },
  },
]);
