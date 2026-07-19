import { createElement } from "react";
import { act, create } from "react-test-renderer";
import { afterEach, describe, expect, it, vi } from "vitest";
import TreeView from "./TreeView";

describe("TreeView render cache", () => {
  afterEach(() => vi.unstubAllGlobals());

  it("does not rebuild nodes when only the viewport moves", () => {
    vi.stubGlobal("Element", class TestElement {});
    const nodes = [{ id: "node-a", x: 10, y: 20, prevIds: [] as string[] }];
    let renderedNodeCount = 0;
    const getId = (node: (typeof nodes)[number]) => node.id;
    const getPosition = (node: (typeof nodes)[number]) => ({ x: node.x, y: node.y });
    const getPrevIds = (node: (typeof nodes)[number]) => node.prevIds;
    const renderNode = () => {
      renderedNodeCount++;
      return createElement("span", null, "node");
    };
    const renderer = create(createElement(TreeView, {
      nodes,
      getId,
      getPosition,
      getPrevIds,
      renderNode,
      nodeTargetSelector: "[data-node]",
      testIdPrefix: "test",
    }));
    const viewport = renderer.root.findByProps({ "data-testid": "test-viewport" });
    expect(renderedNodeCount).toBe(1);

    // viewport状態だけを更新し、静的ノードの再構築有無を観測する
    // Update only viewport state and observe whether static nodes rebuild
    act(() => viewport.props.onPointerDown({
      isPrimary: true,
      button: 0,
      target: null,
      pointerId: 1,
      clientX: 0,
      clientY: 0,
      currentTarget: { setPointerCapture: () => undefined },
    }));
    act(() => viewport.props.onPointerMove({
      pointerId: 1,
      clientX: 10,
      clientY: 5,
      currentTarget: {
        offsetWidth: 100,
        getBoundingClientRect: () => ({ width: 100 }),
      },
    }));

    expect(renderedNodeCount).toBe(1);
  });
});
