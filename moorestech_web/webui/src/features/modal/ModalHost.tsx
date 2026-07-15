import { useState } from "react";
import { Button, Modal, Text, TextInput } from "@mantine/core";
import { useTopic, dispatchAction, Topics } from "@/bridge";
import type { ModalRequest } from "@/bridge/contract/payloadTypes";
import { respondPayload, buttonColor, canConfirm } from "./modalLogic";

// uGUI OneButtonModal の web 版。ui.modal トピックを購読し、要求があれば中央モーダルを描く。
// Web version of uGUI OneButtonModal; subscribes ui.modal and renders a centered modal on request.
export function ModalHost() {
  const data = useTopic(Topics.modal);

  // スナップショット未着、または表示対象が無ければ何も描かない。
  // Render nothing before the first snapshot or when there is no modal to show.
  if (!data || !data.modal) return null;

  // id を key にして要求ごとに入力状態をリセットする。
  // Keying by id resets the input state per request.
  return <ModalBody key={data.modal.id} modal={data.modal} />;
}

function ModalBody({ modal }: { modal: ModalRequest }) {
  const { id, title, message, buttonText, variant, input } = modal;
  const [text, setText] = useState("");

  // confirm/cancel を host へ送る。input モーダルの confirm は Trim 済み text を同送する。
  // Send confirm/cancel to the host; an input modal's confirm carries the trimmed text.
  const confirm = () => dispatchAction("ui.modal.respond", respondPayload(id, "confirm", input ? text.trim() : undefined));
  const cancel = () => dispatchAction("ui.modal.respond", respondPayload(id, "cancel"));

  // e2e が同期検証できるようトランジションは無効化する。
  // Disable transitions so e2e can assert synchronously.
  return (
    <Modal.Root opened onClose={() => void cancel()} centered withinPortal={false} transitionProps={{ duration: 0 }}>
      <Modal.Overlay data-testid="modal-backdrop" backgroundOpacity={0.6} />
      <Modal.Content data-testid="modal" w={320}>
        {/* Header+Title で Content に aria-labelledby を自動配線（uGUI titleText 相当） */}
        {/* Header+Title auto-wires aria-labelledby onto Content (mirrors uGUI titleText) */}
        <Modal.Header>
          <Modal.Title fz="h4" fw={700}>{title}</Modal.Title>
        </Modal.Header>
        <Modal.Body p="lg">
          {/* 本文。uGUI の descriptionText に対応 */}
          {/* Body text, mapping to uGUI descriptionText */}
          <Text size="sm" c="dimmed" mb="lg">{message}</Text>
          {input ? (
            <TextInput data-testid="modal-input" value={text} onChange={(e) => setText(e.currentTarget.value)} mb="lg" autoFocus />
          ) : null}
          <Button data-testid="modal-button" fullWidth color={buttonColor(variant)} disabled={!canConfirm(input, text)} onClick={() => void confirm()}>
            {buttonText}
          </Button>
        </Modal.Body>
      </Modal.Content>
    </Modal.Root>
  );
}
