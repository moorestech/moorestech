export { I18nProvider } from "./I18nProvider";
export { LocalizedShortcutHint } from "./LocalizedShortcutHint";
export {
  blockNameKey,
  buildMenuCategoryNameKey,
  buildMenuSubCategoryNameKey,
  challengeCategoryNameKey,
  challengeSummaryKey,
  challengeTitleKey,
  characterNameKey,
  itemNameKey,
  researchNodeDescriptionKey,
  researchNodeNameKey,
} from "./contentKeys";
export { L } from "./generated/localizationKeys";
export { isTranslationKey, translateExternalKey, useI18n } from "./i18nStore";
export { useItemNameResolver } from "./itemName/useItemName";
export type { ContentLocalizationKey } from "./contentKeys";
export type { InterpolationValues, TranslationDictionary, TranslationKey } from "./i18nStore";
