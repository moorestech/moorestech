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

// 制御欄とstateを描画し変更通知値を保存
// Render the controlled field and state, recording the change-event value
function renderControlledField(tag: "input" | "textarea", initial: string, disabled: boolean, inDisabledFieldset: boolean) {
  function Field() {
    const [value, setValue] = useState(initial);
    const textarea = createElement(tag, {
      "data-testid": "desc",
      value,
      disabled,
      onChange: (e: { currentTarget: HTMLInputElement | HTMLTextAreaElement }) => {
        observed = e.currentTarget.value;
        setValue(e.currentTarget.value);
      },
    });
    return createElement("div", null,
      inDisabledFieldset ? createElement("fieldset", { disabled: true }, textarea) : textarea,
      createElement("output", { "data-testid": "state-value" }, value));
  }
  const container = document.createElement("div");
  document.body.append(container);
  root = createRoot(container);
  act(() => root!.render(createElement(Field)));
  const textarea = container.querySelector(tag)!;
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
  it.each([
    ["input", "", "new"], ["input", "old", "new"], ["input", "old", ""],
    ["textarea", "", "日本語の本文"], ["textarea", "old", "new"], ["textarea", "old", ""],
  ] as const)("制御%sを%sから%sへ置換する", async (tag, initial, text) => {
    renderControlledField(tag, initial, false, false);
    const [action, payload] = await setText("desc", text);
    expect(action).toBe("playtest.dom_set_text_result");
    expect(payload).toMatchObject({ requestId: "r", status: "applied", matchCount: 1, actualValue: text });
    expect(observed).toBe(text);
    expect(document.querySelector<HTMLInputElement | HTMLTextAreaElement>(tag)?.value).toBe(text);
    expect(document.querySelector('[data-testid="state-value"]')?.textContent).toBe(text);
  });

  it.each(["checkbox", "radio", "file", "number", "range", "hidden", "date", "color", "button", "submit", "reset", "image"])("%sは書換前に拒否する", async (type) => {
    const field = document.createElement("input");
    field.type = type;
    field.dataset.testid = "desc";
    document.body.append(field);
    const initial = field.value;
    const [, payload] = await setText("desc", "hello");
    expect(payload).toMatchObject({ status: "notTextField", matchCount: 1 });
    expect(field.value).toBe(initial);
    expect(field.checked).toBe(false);
  });

  it.each(["reset", "removed", "div", "file"])("入力中の%sへのDOM置換を再照会する", async (replacement) => {
    const field = document.createElement("input");
    field.dataset.testid = "desc";
    document.body.append(field);
    vi.spyOn(field, "getBoundingClientRect").mockReturnValue(new DOMRect(10, 10, 100, 20));
    field.addEventListener("input", () => {
      const current = document.createElement(replacement === "div" ? "div" : "input");
      current.dataset.testid = "desc";
      if (current instanceof HTMLInputElement) {
        if (replacement === "file") current.type = "file";
        else current.value = "reset";
      }
      if (replacement === "removed") field.remove();
      else field.replaceWith(current);
    });
    const [, payload] = await setText("desc", "requested");
    const status = replacement === "removed" ? "notFound" : replacement === "reset" ? "valueMismatch" : "notTextField";
    expect(payload).toMatchObject({ status, actualValue: replacement === "reset" ? "reset" : "", matchCount: replacement === "removed" ? 0 : 1 });
    expect(field.isConnected).toBe(false);
  });

  it("disabledの欄はnotInteractableで値を変えない", async () => {
    renderControlledField("textarea", "", true, false);
    const [, payload] = await setText("desc", "x");
    expect(payload).toMatchObject({ status: "notInteractable", actualValue: "" });
  });

  it("disabled fieldset内の欄はnotInteractableで値を変えない", async () => {
    renderControlledField("textarea", "", false, true);
    const textarea = document.querySelector<HTMLTextAreaElement>("textarea")!;
    expect(textarea.disabled).toBe(false);
    expect(textarea.matches(":disabled")).toBe(true);
    const [, payload] = await setText("desc", "injected");
    expect(payload).toMatchObject({ status: "notInteractable", actualValue: "" });
    expect(textarea.value).toBe("");
    expect(document.querySelector('[data-testid="state-value"]')?.textContent).toBe("");
  });

  it("テキスト欄でない要素はnotTextField", async () => {
    const div = document.createElement("div");
    div.dataset.testid = "plain";
    document.body.append(div);
    const [, payload] = await setText("plain", "x");
    expect(payload).toMatchObject({ status: "notTextField" });
  });

  it("重複testidではどの欄も書き換えずambiguousを返す", async () => {
    const inputs = ["first", "second"].map((value) => {
      const input = document.createElement("input");
      input.dataset.testid = "desc";
      input.value = value;
      document.body.append(input);
      vi.spyOn(input, "getBoundingClientRect").mockReturnValue(new DOMRect(10, 10, 100, 20));
      return input;
    });
    const [, payload] = await setText("desc", "requested");
    expect(payload).toMatchObject({ status: "ambiguous", matchCount: 2 });
    expect(inputs.map((input) => input.value)).toEqual(["first", "second"]);
  });

  it("見つからなければnotFound", async () => {
    const [, payload] = await setText("missing", "x");
    expect(payload).toMatchObject({ status: "notFound", matchCount: 0, actualValue: "" });
  });
});
