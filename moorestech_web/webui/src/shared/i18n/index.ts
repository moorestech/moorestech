export { I18nProvider } from "./I18nProvider";
export { LocalizedShortcutHint } from "./LocalizedShortcutHint";
export {
  blockNameKey,
  buildMenuCategoryNameKey,
  buildMenuSubCategoryNameKey,
  challengeCategoryDescriptionKey,
  challengeCategoryNameKey,
  challengeSummaryKey,
  challengeTitleKey,
  characterNameKey,
  itemNameKey,
  researchDescriptionKey,
  researchNameKey,
} from "./contentKeys";
export { L } from "./generated/localizationKeys";
export { isTranslationKey, translateExternalKey, useI18n } from "./i18nStore";
export { useItemNameResolver } from "./itemName/useItemName";
export type { ContentLocalizationKey } from "./contentKeys";
export type { InterpolationValues, TranslationDictionary, TranslationKey } from "./i18nStore";
