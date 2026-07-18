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
  it("現在値と要求値を指定の区切りと通常色で表示する", () => {
    const markup = renderLackHighlightText({
      label: "トルク ",
      current: "1.0",
      separator: " / ",
      required: "2.0",
      insufficient: false,
      normalColor: "dark.1",
      insufficientColor: "red.5",
      size: "sm",
    });

    expect(markup).toContain("トルク 1.0 / 2.0");
    expect(markup).toContain("var(--mantine-color-dark-1)");
  });

  it("不足時に指定した赤色を使う", () => {
    const markup = renderLackHighlightText({
      label: "RPM ",
      current: "1.0",
      separator: " / ",
      required: "2.0",
      insufficient: true,
      normalColor: "dark.1",
      insufficientColor: "red.5",
      size: "sm",
    });

    expect(markup).toContain("var(--mantine-color-red-5)");
  });
});
