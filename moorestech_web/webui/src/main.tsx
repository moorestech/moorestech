import { StrictMode } from "react";
import { createRoot } from "react-dom/client";
import { MantineProvider } from "@mantine/core";
// Mantine のグローバル CSS より index.css を後に読み、CEF 透過の body 背景を勝たせる
// Load index.css after Mantine globals so the CEF-transparent body background wins
import "@mantine/core/styles.css";
import App from "@/app/App";
import { AppErrorBoundary } from "@/app/AppErrorBoundary";
import "@/app/index.css";
import { initBridge, setToastSink } from "@/bridge";
import { initDomQueryResponder } from "./bridge/playtest/domQueryResponder";
import { emitToast } from "@/features/toast";
import { I18nProvider } from "@/shared/i18n";

// bridge の通知 sink に toast store を注入（bridge→features の逆依存を作らない）
// Inject the toast store into the bridge notify sink (avoids a bridge→features back-dependency)
setToastSink(emitToast);

initBridge();
// 通常topicの復元後にプレイテスト用DOM問い合わせの待受を開始する
// Start listening for playtest DOM queries after regular topics restore
initDomQueryResponder();

createRoot(document.getElementById("root")!).render(
  <StrictMode>
    <MantineProvider defaultColorScheme="dark">
      <AppErrorBoundary>
        <I18nProvider>
          <App />
        </I18nProvider>
      </AppErrorBoundary>
    </MantineProvider>
  </StrictMode>
);
