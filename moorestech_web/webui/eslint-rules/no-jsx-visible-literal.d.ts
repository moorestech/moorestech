export const noJsxVisibleLiteral: {
  create(context: {
    report(descriptor: { node: unknown; messageId: string }): void;
  }): {
    JSXText(node: { value: string }): void;
    JSXExpressionContainer(node: {
      expression: Record<string, unknown>;
    }): void;
    JSXAttribute(node: {
      name: { type: string; name?: string };
      value?: { type: string; value?: unknown } | null;
    }): void;
  };
};
