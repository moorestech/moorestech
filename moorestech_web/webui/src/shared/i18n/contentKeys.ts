export type ContentLocalizationKey =
  | `item.${string}.name`
  | `block.${string}.name`
  | `research.${string}.name`
  | `research.${string}.description`
  | `challenge.${string}.title`
  | `challenge.${string}.summary`
  | `challengeCategory.${string}.name`
  | `character.${string}.name`
  | `buildMenuCategory.${string}.name`
  | `buildMenuSubCategory.${string}.name`;

export const itemNameKey = (guid: string): ContentLocalizationKey => `item.${guid}.name`;
export const blockNameKey = (guid: string): ContentLocalizationKey => `block.${guid}.name`;
export const researchNodeNameKey = (guid: string): ContentLocalizationKey => `research.${guid}.name`;
export const researchNodeDescriptionKey = (guid: string): ContentLocalizationKey =>
  `research.${guid}.description`;
export const challengeTitleKey = (guid: string): ContentLocalizationKey =>
  `challenge.${guid}.title`;
export const challengeSummaryKey = (guid: string): ContentLocalizationKey =>
  `challenge.${guid}.summary`;
export const challengeCategoryNameKey = (guid: string): ContentLocalizationKey =>
  `challengeCategory.${guid}.name`;
export const characterNameKey = (guid: string): ContentLocalizationKey => `character.${guid}.name`;
export const buildMenuCategoryNameKey = (guid: string): ContentLocalizationKey =>
  `buildMenuCategory.${guid}.name`;
export const buildMenuSubCategoryNameKey = (guid: string): ContentLocalizationKey =>
  `buildMenuSubCategory.${guid}.name`;

// skitキーはUnity側resolverで解決済みの表示文字列をpushするためWebから構築しない
// Skit keys are resolved by the Unity-side resolver before display strings are pushed
