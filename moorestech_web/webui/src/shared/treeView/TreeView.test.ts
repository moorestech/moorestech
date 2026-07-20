import { createElement } from "react";
import { act, create } from "react-test-renderer";
import { afterEach, describe, expect, it, vi } from "vitest";
import TreeView from "./TreeView";

type TestNode = { id: string; x: number; y: number; prevIds: string[] };

describe("TreeView render cache", () => {
  afterEach(() => vi.unstubAllGlobals());

  it("does not rebuild nodes when only the viewport moves", () => {
    vi.stubGlobal("Element", class TestElement {});
    const nodes: TestNode[] = [
      { id: "node-a", x: 10, y: 20, prevIds: [] },
      { id: "node-b", x: 30, y: 40, prevIds: ["node-a"] },
    ];
    let renderedNodeCount = 0;
    let positionReadCount = 0;
    let previousIdsReadCount = 0;
    const getId = (node: TestNode) => node.id;
    const getPosition = (node: TestNode) => {
      positionReadCount++;
      return { x: node.x, y: node.y };
    };
    const getPrevIds = (node: TestNode) => {
      previousIdsReadCount++;
      return node.prevIds;
    };
    const renderNode = () => {
      renderedNodeCount++;
      return createElement("span", null, "node");
    };
    const renderer = create(createElement(TreeView<TestNode>, {
      nodes,
      getId,
      getPosition,
      getPrevIds,
      renderNode,
      nodeTargetSelector: "[data-node]",
      testIdPrefix: "test",
    }));
    const viewport = renderer.root.findByProps({ "data-testid": "test-viewport" });
    const initialPositionReadCount = positionReadCount;
    const initialPreviousIdsReadCount = previousIdsReadCount;
    expect(renderedNodeCount).toBe(2);

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

    const canvas = renderer.root.findByProps({ "data-testid": "test-canvas" });
    expect(renderedNodeCount).toBe(2);
    expect(positionReadCount).toBe(initialPositionReadCount);
    expect(previousIdsReadCount).toBe(initialPreviousIdsReadCount);
    expect(canvas.props.style.transform).toBe("translate(10px, 5px) scale(1)");
  });

  it("rebuilds the scene when render inputs change", () => {
    const firstNodes: TestNode[] = [{ id: "node-a", x: 10, y: 20, prevIds: [] }];
    const secondNodes: TestNode[] = [{ id: "node-b", x: 30, y: 40, prevIds: [] }];
    const getId = (node: TestNode) => node.id;
    const getPosition = (node: TestNode) => ({ x: node.x, y: node.y });
    const getPrevIds = (node: TestNode) => node.prevIds;
    const firstRenderNode = vi.fn(() => createElement("span", null, "first"));
    const secondRenderNode = vi.fn(() => createElement("span", null, "second"));
    const renderer = create(createElement(TreeView<TestNode>, {
      nodes: firstNodes,
      getId,
      getPosition,
      getPrevIds,
      renderNode: firstRenderNode,
      nodeTargetSelector: "[data-node]",
      testIdPrefix: "test",
    }));

    act(() => renderer.update(createElement(TreeView<TestNode>, {
      nodes: secondNodes,
      getId,
      getPosition,
      getPrevIds,
      renderNode: secondRenderNode,
      nodeTargetSelector: "[data-node]",
      testIdPrefix: "test",
    })));

    expect(firstRenderNode).toHaveBeenCalledOnce();
    expect(secondRenderNode).toHaveBeenCalledOnce();
    expect(secondRenderNode).toHaveBeenCalledWith(secondNodes[0], expect.any(Object));
  });
});
