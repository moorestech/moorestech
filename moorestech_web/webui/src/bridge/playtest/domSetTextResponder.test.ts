// @vitest-environment jsdom
// ネイティブsetterとinputイベントには実DOMが要るためreact-dom/clientで描画する
// Native setters and input events require real DOM rendering with react-dom/client
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { createElement, useState, act } from "react";
import { createRoot, type Root } from "react-dom/client";
import { useTopicStore } from "../store/topicStore";
import { Topics } from "../transport/protocol";
import { sendAction } from "../transport/webSocketClient";
import { initDomSetTextResponder } from "./domSetTextResponder";

vi.mock("../transport/webSocketClient", () => ({ sendAction: vi.fn().mockResolvedValue({ ok: true }) }));
vi.mock("../transport/subscriptionManager", () => ({ subscriptions: { acquire: vi.fn(), release: vi.fn() } }));

let dispose: () => void;
let root: Root | null = null;
let observed = "";

beforeEach(() => {
  vi.mocked(sendAction).mockClear().mockResolvedValue({ ok: true });
  vi.stubGlobal("CSS", { escape: (value: string) => value });
  vi.stubGlobal("IS_REACT_ACT_ENVIRONMENT", true);
  useTopicStore.setState({ topics: {}, status: "open" });
  observed = "";
  const defaults = document.createElement("style");
  defaults.textContent = "* { position: static; overflow: visible; overflow-x: visible; overflow-y: visible; transform: none; filter: none; perspective: none; backdrop-filter: none; will-change: auto; contain: none; clip-path: none; border-width: 0px; }";
  document.body.append(defaults);
  dispose = initDomSetTextResponder();
});

afterEach(() => {
  dispose();
  act(() => root?.unmount());
  root = null;
  document.body.replaceChildren();
  vi.restoreAllMocks();
  vi.unstubAllGlobals();
});

// 制御textareaを描画し、onChangeの観測値を保存する
// Render a controlled textarea and record its onChange value
function renderControlledTextarea(disabled: boolean) {
  function Field() {
    const [value, setValue] = useState("");
    return createElement("textarea", {
      "data-testid": "desc",
      value,
      disabled,
      onChange: (e: { currentTarget: HTMLTextAreaElement }) => {
        observed = e.currentTarget.value;
        setValue(e.currentTarget.value);
      },
    });
  }
  const container = document.createElement("div");
  document.body.append(container);
  root = createRoot(container);
  act(() => root!.render(createElement(Field)));
  const textarea = container.querySelector("textarea")!;
  vi.spyOn(textarea, "getBoundingClientRect").mockReturnValue(new DOMRect(10, 10, 100, 20));
}

async function setText(testid: string, text: string) {
  await act(async () => {
    useTopicStore.setState({ topics: { [Topics.playtestDomSetText]: { requestId: "r", testid, text } } });
    await new Promise((resolve) => setTimeout(resolve, 5));
  });
  return vi.mocked(sendAction).mock.calls[0];
}

describe("プレイテストの文字入力", () => {
  it("制御textareaのonChangeが走り、値が保たれればappliedを返す", async () => {
    renderControlledTextarea(false);
    const [action, payload] = await setText("desc", "日本語の本文");
    expect(action).toBe("playtest.dom_set_text_result");
    expect(payload).toMatchObject({ requestId: "r", status: "applied", matchCount: 1, actualValue: "日本語の本文" });
    expect(observed).toBe("日本語の本文");
  });

  it("disabledの欄はnotInteractableで値を変えない", async () => {
    renderControlledTextarea(true);
    const [, payload] = await setText("desc", "x");
    expect(payload).toMatchObject({ status: "notInteractable", actualValue: "" });
  });

  it("テキスト欄でない要素はnotTextField", async () => {
    const div = document.createElement("div");
    div.dataset.testid = "plain";
    document.body.append(div);
    const [, payload] = await setText("plain", "x");
    expect(payload).toMatchObject({ status: "notTextField" });
  });

  it("見つからなければnotFound", async () => {
    const [, payload] = await setText("missing", "x");
    expect(payload).toMatchObject({ status: "notFound", matchCount: 0, actualValue: "" });
  });
});
