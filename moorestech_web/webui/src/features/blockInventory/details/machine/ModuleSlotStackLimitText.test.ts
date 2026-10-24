import { createElement } from "react";
import { create } from "react-test-renderer";
import { describe, expect, it, vi } from "vitest";

const topic = vi.hoisted(() => ({ value: null as { moduleSlotStackLimit: number } | null }));

vi.mock("@/bridge", async (importOriginal) => ({
  ...(await importOriginal<typeof import("@/bridge")>()),
  useTopic: () => topic.value,
}));
vi.mock("@/shared/i18n", async (importOriginal) => ({
  ...(await importOriginal<typeof import("@/shared/i18n")>()),
  useI18n: () => ({ t: (key: string, values?: Record<string, unknown>) => `${key}:${String(values?.limit)}` }),
}));
vi.mock("@mantine/core", () => ({
  Text: ({ children, ...props }: { children: unknown }) => createElement("mock-text", props, children as never),
}));

import ModuleSlotStackLimitText from "./ModuleSlotStackLimitText";

describe("ModuleSlotStackLimitText", () => {
  it("上限の配信が届くまでは何も描かない", () => {
    topic.value = null;
    const tree = create(createElement(ModuleSlotStackLimitText));
    expect(tree.toJSON()).toBeNull();
  });

  it("配信された上限を差し込んで描く", () => {
    topic.value = { moduleSlotStackLimit: 6 };
    const tree = create(createElement(ModuleSlotStackLimitText));
    expect(tree.root.findByProps({ "data-testid": "machine-module-stack-limit" }).props.children).toBe("ui.blockInventory.moduleSlotStackLimit:6");
  });
});
