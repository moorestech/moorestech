import { StrictMode } from "react";
import { createRoot } from "react-dom/client";
import App from "./App";
import "./index.css";
import { setToastSink } from "./bridge/notify";
import { emitToast } from "./features/toast/toastStore";

// bridge の通知 sink に toast store を注入（bridge→features の逆依存を作らない）
// Inject the toast store into the bridge notify sink (avoids a bridge→features back-dependency)
setToastSink(emitToast);

createRoot(document.getElementById("root")!).render(
  <StrictMode>
    <App />
  </StrictMode>
);
