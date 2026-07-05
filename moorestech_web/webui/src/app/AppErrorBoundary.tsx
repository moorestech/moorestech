import { Component, type ReactNode } from "react";
import { Button, Stack, Text, Title } from "@mantine/core";

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
    return (
      <Stack align="center" justify="center" h="100vh" gap="md" p="lg">
        <Title order={2} size="h3">UIエラーが発生しました</Title>
        <Text size="sm" c="dimmed" ta="center">画面の描画中に問題が発生しました。再読み込みしてください。</Text>
        <Button color="red" onClick={() => location.reload()}>再読み込み</Button>
      </Stack>
    );
  }
}
