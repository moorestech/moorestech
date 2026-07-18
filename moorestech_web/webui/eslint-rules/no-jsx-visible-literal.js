const VISIBLE_ATTRIBUTES = new Set(["alt", "aria-label", "placeholder", "title"]);

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
        if (containsStringLiteral(node.expression)) report(node, "literal");
      },
      JSXAttribute(node) {
        if (node.name.type === "JSXIdentifier" && VISIBLE_ATTRIBUTES.has(node.name.name) && node.value?.type === "Literal") {
          report(node, node.value.value);
        }
      },
    };
  },
};

function containsStringLiteral(node) {
  if (node === null || typeof node !== "object") return false;
  if (node.type === "Literal") return typeof node.value === "string" && node.value.trim().length > 0;
  if (node.type === "TemplateElement") return node.value.raw.trim().length > 0;

  return Object.entries(node).some(([key, value]) => {
    if (key === "parent" || key === "loc" || key === "range") return false;
    if (Array.isArray(value)) return value.some(containsStringLiteral);
    return containsStringLiteral(value);
  });
}
