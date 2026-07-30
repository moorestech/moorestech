export type ContentLocalizationKey =
  | `item.${string}.name`
  | `block.${string}.name`
  | `research.${string}.name`
  | `research.${string}.description`
  | `challenge.${string}.title`
  | `challenge.${string}.summary`
  | `challengeCategory.${string}.name`
  | `challengeCategory.${string}.description`
  | `character.${string}.name`
  | `buildMenuCategory.${string}.name`
  | `buildMenuSubCategory.${string}.name`;

export const itemNameKey = (guid: string): ContentLocalizationKey =>
  `item.${canonicalGuidSegment(guid)}.name`;
export const blockNameKey = (guid: string): ContentLocalizationKey =>
  `block.${canonicalGuidSegment(guid)}.name`;
export const researchNodeNameKey = (guid: string): ContentLocalizationKey =>
  `research.${canonicalGuidSegment(guid)}.name`;
export const researchNodeDescriptionKey = (guid: string): ContentLocalizationKey =>
  `research.${canonicalGuidSegment(guid)}.description`;
export const challengeTitleKey = (guid: string): ContentLocalizationKey =>
  `challenge.${canonicalGuidSegment(guid)}.title`;
export const challengeSummaryKey = (guid: string): ContentLocalizationKey =>
  `challenge.${canonicalGuidSegment(guid)}.summary`;
export const challengeCategoryNameKey = (guid: string): ContentLocalizationKey =>
  `challengeCategory.${canonicalGuidSegment(guid)}.name`;
export const challengeCategoryDescriptionKey = (guid: string): ContentLocalizationKey =>
  `challengeCategory.${canonicalGuidSegment(guid)}.description`;
export const characterNameKey = (guid: string): ContentLocalizationKey =>
  `character.${canonicalGuidSegment(guid)}.name`;
export const buildMenuCategoryNameKey = (guid: string): ContentLocalizationKey =>
  `buildMenuCategory.${canonicalGuidSegment(guid)}.name`;
export const buildMenuSubCategoryNameKey = (guid: string): ContentLocalizationKey =>
  `buildMenuSubCategory.${canonicalGuidSegment(guid)}.name`;

// skitキーはUnity側resolverで解決済みの表示文字列をpushするためWebから構築しない
// Skit keys are resolved by the Unity-side resolver before display strings are pushed

function canonicalGuidSegment(guid: string): string {
  return guid.toLowerCase();
}
