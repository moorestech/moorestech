import { createElement } from "react";
import { renderToStaticMarkup } from "react-dom/server";
import { MantineProvider } from "@mantine/core";
import { describe, expect, it } from "vitest";
import LackHighlightText from "./index";

function renderLackHighlightText(props: Parameters<typeof LackHighlightText>[0]) {
  return renderToStaticMarkup(
    createElement(MantineProvider, null, createElement(LackHighlightText, props)),
  );
}

describe("LackHighlightText", () => {
  it("現在値と要求値を表示し、通常状態をdata属性で公開する", () => {
    const markup = renderLackHighlightText({
      label: "トルク ",
      current: "1.0",
      separator: " / ",
      required: "2.0",
      insufficient: false,
      size: "sm",
      testId: "torque",
    });

    expect(markup).toContain("トルク 1.0 / 2.0");
    expect(markup).toContain('data-testid="torque"');
    expect(markup).toContain('data-insufficient="false"');
  });

  it("不足状態をdata属性で公開する", () => {
    const markup = renderLackHighlightText({
      label: "RPM ",
      current: "1.0",
      separator: " / ",
      required: "2.0",
      insufficient: true,
      size: "sm",
    });

    expect(markup).toContain('data-insufficient="true"');
  });
});
