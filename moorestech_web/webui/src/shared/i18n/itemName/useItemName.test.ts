import { createElement } from "react";
import { act, create, type ReactTestRenderer } from "react-test-renderer";
import { beforeEach, describe, expect, it, vi } from "vitest";
import type { ItemMasterEntry } from "@/bridge";
import { itemNameKey } from "../contentKeys";
import { L } from "../generated/localizationKeys";
import { setDictionaries } from "../i18nStore";

const ITEM_GUID = "01234567-89ab-cdef-0123-456789abcdef";
const mockState = vi.hoisted(() => ({
  master: null as Map<number, ItemMasterEntry> | null,
}));

vi.mock("@/bridge", async (importOriginal) => ({
  ...await importOriginal<typeof import("@/bridge")>(),
  useItemMaster: () => mockState.master,
}));

import { useItemDisplayName, useItemNameResolver } from "./useItemName";

const FALLBACK_TEMPLATE = "アイテム {itemId}";

let latestResolver: ReturnType<typeof useItemNameResolver>;

function ResolverProbe({ itemId, revision }: { itemId: number; revision: number }) {
  latestResolver = useItemNameResolver();
  return createElement("span", { "data-revision": revision }, latestResolver(itemId));
}

function renderProbe(itemId: number): ReactTestRenderer {
  let renderer!: ReactTestRenderer;
  act(() => {
    renderer = create(createElement(ResolverProbe, { itemId, revision: 0 }));
  });
  return renderer;
}

describe("useItemNameResolver", () => {
  beforeEach(() => {
    mockState.master = null;
    setDictionaries("japanese", {}, {}, {});
  });

  it("マスタ未着ではnullを返す", () => {
    const renderer = renderProbe(1);
    expect(latestResolver(1)).toBeNull();
    renderer.unmount();
  });

  it("未知のitemIdではnullを返す", () => {
    mockState.master = new Map([[1, { itemId: 1, itemGuid: ITEM_GUID, maxStack: 100 }]]);
    const renderer = renderProbe(2);
    expect(latestResolver(2)).toBeNull();
    renderer.unmount();
  });

  it("itemIdからGuidを引き対象言語名を解決する", () => {
    const key = itemNameKey(ITEM_GUID);
    mockState.master = new Map([[1, { itemId: 1, itemGuid: ITEM_GUID, maxStack: 100 }]]);
    setDictionaries("japanese", { [key]: "対象名" }, { [key]: "English name" }, { [key]: "Source name" });

    const renderer = renderProbe(1);

    expect(renderer.root.findByType("span").children).toEqual(["対象名"]);
    renderer.unmount();
  });

  it("対象言語が空なら英語辞書へフォールバックする", () => {
    const key = itemNameKey(ITEM_GUID);
    mockState.master = new Map([[1, { itemId: 1, itemGuid: ITEM_GUID, maxStack: 100 }]]);
    setDictionaries("japanese", { [key]: "" }, { [key]: "English name" }, { [key]: "Source name" });

    const renderer = renderProbe(1);

    expect(renderer.root.findByType("span").children).toEqual(["English name"]);
    renderer.unmount();
  });

  it("辞書更新で再描画し同じGuidを新しい言語名へ切り替える", () => {
    const key = itemNameKey(ITEM_GUID);
    mockState.master = new Map([[1, { itemId: 1, itemGuid: ITEM_GUID, maxStack: 100 }]]);
    setDictionaries("japanese", { [key]: "日本語名" }, { [key]: "English name" }, { [key]: "Source name" });
    const renderer = renderProbe(1);

    act(() => {
      setDictionaries("english", { [key]: "English name" }, { [key]: "English name" }, { [key]: "Source name" });
    });

    expect(renderer.root.findByType("span").children).toEqual(["English name"]);
    renderer.unmount();
  });

  it("辞書とマスタが同じ再描画ではresolver参照を維持する", () => {
    const key = itemNameKey(ITEM_GUID);
    mockState.master = new Map([[1, { itemId: 1, itemGuid: ITEM_GUID, maxStack: 100 }]]);
    setDictionaries("english", { [key]: "English name" }, { [key]: "English name" }, { [key]: "Source name" });
    const renderer = renderProbe(1);
    const firstResolver = latestResolver;

    act(() => {
      renderer.update(createElement(ResolverProbe, { itemId: 1, revision: 1 }));
    });

    expect(latestResolver).toBe(firstResolver);
    renderer.unmount();
  });
});

function DisplayNameProbe({ itemId }: { itemId: number }) {
  return createElement("span", null, useItemDisplayName()(itemId));
}

function renderDisplayNameProbe(itemId: number): ReactTestRenderer {
  let renderer!: ReactTestRenderer;
  act(() => {
    renderer = create(createElement(DisplayNameProbe, { itemId }));
  });
  return renderer;
}

describe("useItemDisplayName", () => {
  beforeEach(() => {
    mockState.master = null;
    setDictionaries("japanese", { [L.ui.common.itemFallback]: FALLBACK_TEMPLATE }, {}, {});
  });

  it("マスタ未着ではid表示へ落とす", () => {
    const renderer = renderDisplayNameProbe(2);
    expect(renderer.root.findByType("span").children).toEqual(["アイテム 2"]);
    renderer.unmount();
  });

  it("解決できれば表示名を返す", () => {
    const key = itemNameKey(ITEM_GUID);
    mockState.master = new Map([[1, { itemId: 1, itemGuid: ITEM_GUID, maxStack: 100 }]]);
    setDictionaries("japanese", { [L.ui.common.itemFallback]: FALLBACK_TEMPLATE, [key]: "石" }, {}, {});

    const renderer = renderDisplayNameProbe(1);

    expect(renderer.root.findByType("span").children).toEqual(["石"]);
    renderer.unmount();
  });
});
