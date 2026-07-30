import { describe, expect, it } from "vitest";
import {
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
  type ContentLocalizationKey,
} from "./contentKeys";
import type { TranslationKey } from "./i18nStore";

describe("content localization keys", () => {
  it("builds every supported non-skit content key with exact casing", () => {
    const guid = "01234567-89AB-CDEF-0123-456789ABCDEF";

    expect(itemNameKey(guid)).toBe("item.01234567-89ab-cdef-0123-456789abcdef.name");
    expect(blockNameKey(guid)).toBe("block.01234567-89ab-cdef-0123-456789abcdef.name");
    expect(researchNodeNameKey(guid)).toBe("research.01234567-89ab-cdef-0123-456789abcdef.name");
    expect(researchNodeDescriptionKey(guid)).toBe("research.01234567-89ab-cdef-0123-456789abcdef.description");
    expect(challengeTitleKey(guid)).toBe("challenge.01234567-89ab-cdef-0123-456789abcdef.title");
    expect(challengeSummaryKey(guid)).toBe("challenge.01234567-89ab-cdef-0123-456789abcdef.summary");
    expect(challengeCategoryNameKey(guid)).toBe("challengeCategory.01234567-89ab-cdef-0123-456789abcdef.name");
    expect(characterNameKey(guid)).toBe("character.01234567-89ab-cdef-0123-456789abcdef.name");
    expect(buildMenuCategoryNameKey(guid)).toBe("buildMenuCategory.01234567-89ab-cdef-0123-456789abcdef.name");
    expect(buildMenuSubCategoryNameKey(guid)).toBe("buildMenuSubCategory.01234567-89ab-cdef-0123-456789abcdef.name");
  });

  it("merges content keys into the translation key contract", () => {
    const contentKey: ContentLocalizationKey = challengeCategoryNameKey("category-guid");
    const translationKey: TranslationKey = contentKey;

    expect(translationKey).toBe("challengeCategory.category-guid.name");
  });

  it("rejects strings outside the content key grammar", () => {
    const acceptContentKey = (key: ContentLocalizationKey): ContentLocalizationKey => key;

    // @ts-expect-error -- A content key must match one of the supported namespaces.
    acceptContentKey("not-a-localization-key");
    // @ts-expect-error -- Item keys only expose the name field.
    acceptContentKey("item.guid.title");
  });
});
