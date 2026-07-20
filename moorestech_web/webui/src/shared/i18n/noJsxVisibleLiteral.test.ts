import { describe, expect, it, vi } from "vitest";
import { noJsxVisibleLiteral } from "../../../eslint-rules/no-jsx-visible-literal.js";

function createVisitors() {
  const report = vi.fn();
  const visitors = noJsxVisibleLiteral.create({ report });
  return { report, visitors };
}

describe("no-jsx-visible-literal", () => {
  it("reports visible JSX text and ignores layout whitespace", () => {
    const { report, visitors } = createVisitors();
    visitors.JSXText({ value: "New screen title" });
    visitors.JSXText({ value: "\n  " });
    expect(report).toHaveBeenCalledTimes(1);
  });

  it("reports user-visible literal attributes but not implementation attributes", () => {
    const { report, visitors } = createVisitors();
    visitors.JSXAttribute({
      name: { type: "JSXIdentifier", name: "aria-label" },
      value: { type: "Literal", value: "Close" },
    });
    visitors.JSXAttribute({
      name: { type: "JSXIdentifier", name: "data-testid" },
      value: { type: "Literal", value: "close-button" },
    });
    expect(report).toHaveBeenCalledTimes(1);
  });

  it("reports literals nested inside JSX expressions", () => {
    const { report, visitors } = createVisitors();
    visitors.JSXExpressionContainer({
      expression: {
        type: "ConditionalExpression",
        test: { type: "Identifier", name: "ready" },
        consequent: { type: "Literal", value: "Ready" },
        alternate: { type: "Literal", value: "Waiting" },
      },
    });
    expect(report).toHaveBeenCalledTimes(1);
  });

  it("ignores implementation literals inside JSX elements while still checking rendered logical values", () => {
    const { report, visitors } = createVisitors();
    visitors.JSXExpressionContainer({
      expression: {
        type: "LogicalExpression",
        operator: "&&",
        left: {
          type: "BinaryExpression",
          operator: "===",
          left: { type: "Identifier", name: "screen" },
          right: { type: "Literal", value: "inventory" },
        },
        right: {
          type: "JSXElement",
          openingElement: {
            attributes: [{ type: "JSXAttribute", name: { type: "JSXIdentifier", name: "data-testid" }, value: { type: "Literal", value: "inventory-panel" } }],
          },
        },
      },
    });
    visitors.JSXExpressionContainer({
      expression: {
        type: "LogicalExpression",
        operator: "&&",
        left: { type: "Identifier", name: "ready" },
        right: { type: "Literal", value: "Ready" },
      },
    });
    expect(report).toHaveBeenCalledTimes(1);
  });

  it("ignores string expressions in implementation attributes but checks visible custom props", () => {
    const { report, visitors } = createVisitors();
    visitors.JSXExpressionContainer({
      parent: { type: "JSXAttribute", name: { type: "JSXIdentifier", name: "style" } },
      expression: { type: "TemplateLiteral", quasis: [{ value: { raw: "100%" } }] },
    } as never);
    visitors.JSXExpressionContainer({
      parent: { type: "JSXAttribute", name: { type: "JSXIdentifier", name: "label" } },
      expression: { type: "TemplateLiteral", quasis: [{ value: { raw: "容量: " } }] },
    } as never);
    expect(report).toHaveBeenCalledTimes(1);
  });
});
