const VISIBLE_ATTRIBUTES = new Set(["alt", "aria-label", "placeholder", "title", "label", "separator", "suffix"]);

export const noJsxVisibleLiteral = {
  meta: {
    type: "problem",
    docs: { description: "Require visible JSX copy to come from i18n" },
    schema: [],
    messages: { literal: "Visible JSX text must use the i18n t(key) hook." },
  },
  create(context) {
    const report = (node, value) => {
      if (typeof value === "string" && value.trim().length > 0) {
        context.report({ node, messageId: "literal" });
      }
    };

    return {
      JSXText(node) {
        report(node, node.value);
      },
      JSXExpressionContainer(node) {
        if (node.parent?.type === "JSXAttribute") {
          const attributeName = node.parent.name.type === "JSXIdentifier" ? node.parent.name.name : null;
          if (attributeName === null || !VISIBLE_ATTRIBUTES.has(attributeName)) return;
        }
        if (containsRenderedStringLiteral(node.expression)) report(node, "literal");
      },
      JSXAttribute(node) {
        if (node.name.type === "JSXIdentifier" && VISIBLE_ATTRIBUTES.has(node.name.name) && node.value?.type === "Literal") {
          report(node, node.value.value);
        }
      },
    };
  },
};

function containsRenderedStringLiteral(node) {
  if (node === null || typeof node !== "object") return false;
  if (node.type === "Literal") return typeof node.value === "string" && node.value.trim().length > 0;
  if (node.type === "TemplateLiteral") return node.quasis.some((quasi) => quasi.value.raw.trim().length > 0);
  if (node.type === "ConditionalExpression") {
    return containsRenderedStringLiteral(node.consequent) || containsRenderedStringLiteral(node.alternate);
  }
  if (node.type === "LogicalExpression") return containsRenderedStringLiteral(node.right);
  if (node.type === "SequenceExpression") return containsRenderedStringLiteral(node.expressions.at(-1));
  if (node.type === "BinaryExpression" && node.operator === "+") {
    return containsRenderedStringLiteral(node.left) || containsRenderedStringLiteral(node.right);
  }
  if (node.type === "TSAsExpression" || node.type === "TSNonNullExpression" || node.type === "ChainExpression") {
    return containsRenderedStringLiteral(node.expression);
  }
  return false;
}
