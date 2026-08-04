import { Component, type ReactNode } from "react";
import { Button, Stack, Text, Title } from "@mantine/core";
import { DictionaryIndependentText, L, useI18n } from "@/shared/i18n";

type Props = { children: ReactNode };
type State = { hasError: boolean };

// レンダリング中の例外を捕捉し、UI 全体の白画面クラッシュを防ぐ最後の砦
// Last line of defense: catch render-time exceptions to prevent a blank-screen crash of the whole UI
export class AppErrorBoundary extends Component<Props, State> {
  constructor(props: Props) {
    super(props);
    this.state = { hasError: false };
  }

  static getDerivedStateFromError(): State {
    return { hasError: true };
  }

  componentDidCatch(error: unknown) {
    // 原因調査用にコンソールへ残す（トースト等の副作用はここでは行わない）
    // Log for diagnosis; no side effects like toasts are triggered here
    console.error("[AppErrorBoundary]", error);
  }

  render() {
    if (!this.state.hasError) return this.props.children;

    // フォールバック: エラー通知と再読み込みボタンだけの最小画面
    // Fallback: a minimal screen with just an error notice and a reload button
    return <AppErrorFallback />;
  }
}

function AppErrorFallback() {
  const { status, t } = useI18n();

  // 辞書が未確定なら t() は空文字か欠落マーカーになるため、辞書非依存リテラルへ落とす
  // Before the dictionary is ready t() yields empty text or markers, so fall back to dictionary-independent literals
  const dictionaryReady = status === "ready";
  const title = dictionaryReady ? t(L.ui.error.uiErrorOccurred) : DictionaryIndependentText.uiErrorOccurred;
  const description = dictionaryReady ? t(L.ui.error.renderFailed) : DictionaryIndependentText.renderFailed;
  const reloadLabel = dictionaryReady ? t(L.ui.error.reload) : DictionaryIndependentText.reload;

  return (
    <Stack align="center" justify="center" h="100vh" gap="md" p="lg">
      <Title order={2} size="h3">{title}</Title>
      <Text size="sm" c="dimmed" ta="center">{description}</Text>
      <Button color="red" onClick={() => location.reload()}>{reloadLabel}</Button>
    </Stack>
  );
}
