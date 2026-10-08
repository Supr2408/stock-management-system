import React, { useEffect, useState } from "react";
import type { LabelTemplate, LabelTemplateElement } from "../types";
import { LabelCanvas } from "./LabelCanvas";

interface Props {
  apiUrl: string;
  apiToken: string;
  onNotify: (msg: string) => void;
  activeBarcodePrinter: string | null;
  onRefreshJobs?: () => void;
}

export const TscLabelEditor: React.FC<Props> = ({
  apiUrl,
  apiToken,
  onNotify,
  activeBarcodePrinter,
  onRefreshJobs,
}) => {
  const [templates, setTemplates] = useState<LabelTemplate[]>([]);
  const [selectedTemplateId, setSelectedTemplateId] = useState<number | null>(null);
  const [selectedElementIndex, setSelectedElementIndex] = useState<number | null>(null);
  const [availableLogos, setAvailableLogos] = useState<{ id: number; name: string; fileName: string }[]>([]);
  const [saving, setSaving] = useState(false);
  const [testing, setTesting] = useState(false);

  // Active template draft
  const [draft, setDraft] = useState<LabelTemplate>({
    id: 0,
    name: "Standard TSC 100x50",
    printerType: 2, // TSC
    widthMm: 100,
    heightMm: 50,
    gapMm: 2,
    orientation: 0,
    isDefault: true,
    elements: [
      {
        elementType: 1, // Logo
        xmm: 5,
        ymm: 5,
        widthMm: 25,
        heightMm: 15,
        rotation: 0,
        zIndex: 1,
        fitMode: 1, // Contain
        borderThicknessDots: 1,
        fontSize: 12,
        humanReadable: true,
      },
      {
        elementType: 3, // Text
        xmm: 35,
        ymm: 6,
        widthMm: 60,
        heightMm: 10,
        rotation: 0,
        zIndex: 2,
        content: "{ItemName}",
        fontSize: 14,
        fitMode: 1,
        borderThicknessDots: 1,
        humanReadable: true,
      },
      {
        elementType: 2, // Barcode
        xmm: 15,
        ymm: 22,
        widthMm: 70,
        heightMm: 22,
        rotation: 0,
        zIndex: 3,
        content: "{Barcode}",
        barcodeType: "128",
        humanReadable: true,
        fontSize: 12,
        fitMode: 1,
        borderThicknessDots: 1,
      },
    ],
  });

  const loadTemplates = () => {
    if (!apiToken) return;
    fetch(`${apiUrl}/api/label-templates`, { headers: { Authorization: `Bearer ${apiToken}` } })
      .then((res) => (res.ok ? res.json() : []))
      .then((list: LabelTemplate[]) => {
        setTemplates(list);
        if (list.length > 0 && selectedTemplateId === null) {
          const def = list.find((t) => t.isDefault) ?? list[0];
          setSelectedTemplateId(def.id);
          setDraft(def);
        }
      })
      .catch(() => {});
  };

  const loadLogos = () => {
    if (!apiToken) return;
    fetch(`${apiUrl}/api/labels/logos`, { headers: { Authorization: `Bearer ${apiToken}` } })
      .then((res) => (res.ok ? res.json() : []))
      .then((rows) => setAvailableLogos(rows))
      .catch(() => {});
  };

  useEffect(() => {
    loadTemplates();
    loadLogos();
  }, [apiToken]);

  const handleSelectTemplate = (id: number) => {
    setSelectedTemplateId(id);
    setSelectedElementIndex(null);
    const found = templates.find((t) => t.id === id);
    if (found) setDraft(found);
  };

  const handleNewTemplate = () => {
    setSelectedTemplateId(0);
    setSelectedElementIndex(null);
    setDraft({
      id: 0,
      name: `New TSC Template ${templates.length + 1}`,
      printerType: 2,
      widthMm: 100,
      heightMm: 50,
      gapMm: 2,
      orientation: 0,
      isDefault: false,
      elements: [],
    });
  };

  const handleAddElement = (type: 1 | 2 | 3 | 4) => {
    const newEl: LabelTemplateElement = {
      elementType: type,
      xmm: 5,
      ymm: 5,
      widthMm: type === 1 ? 25 : type === 2 ? 60 : type === 3 ? 40 : 90,
      heightMm: type === 1 ? 15 : type === 2 ? 20 : type === 3 ? 8 : 40,
      rotation: 0,
      zIndex: draft.elements.length + 1,
      fitMode: 1,
      borderThicknessDots: 2,
      fontSize: 12,
      humanReadable: true,
      content: type === 2 ? "{Barcode}" : type === 3 ? "Text element" : undefined,
    };
    const nextElements = [...draft.elements, newEl];
    setDraft({ ...draft, elements: nextElements });
    setSelectedElementIndex(nextElements.length - 1);
  };

  const handleDeleteElement = (index: number) => {
    const next = draft.elements.filter((_, i) => i !== index);
    setDraft({ ...draft, elements: next });
    setSelectedElementIndex(null);
  };

  const handleUpdateElement = (index: number, updated: LabelTemplateElement) => {
    const next = [...draft.elements];
    next[index] = updated;
    setDraft({ ...draft, elements: next });
  };

  const handleSaveTemplate = async () => {
    if (!apiToken) return;
    if (!draft.name.trim()) {
      onNotify("Template name is required.");
      return;
    }

    // Boundary check elements
    for (let i = 0; i < draft.elements.length; i++) {
      const el = draft.elements[i];
      if (el.xmm < 0 || el.ymm < 0) {
        onNotify(`Element #${i + 1} position cannot be negative.`);
        return;
      }
      if (el.xmm + el.widthMm > draft.widthMm + 1.0) {
        onNotify(`Element #${i + 1} extends beyond label width (${(el.xmm + el.widthMm).toFixed(1)}mm > ${draft.widthMm}mm).`);
        return;
      }
      if (el.ymm + el.heightMm > draft.heightMm + 1.0) {
        onNotify(`Element #${i + 1} extends beyond label height (${(el.ymm + el.heightMm).toFixed(1)}mm > ${draft.heightMm}mm).`);
        return;
      }
    }

    setSaving(true);
    try {
      const isUpdate = draft.id > 0;
      const url = isUpdate ? `${apiUrl}/api/label-templates/${draft.id}` : `${apiUrl}/api/label-templates`;
      const method = isUpdate ? "PUT" : "POST";

      const res = await fetch(url, {
        method,
        headers: { Authorization: `Bearer ${apiToken}`, "Content-Type": "application/json" },
        body: JSON.stringify({
          name: draft.name,
          printerType: draft.printerType,
          widthMm: draft.widthMm,
          heightMm: draft.heightMm,
          gapMm: draft.gapMm,
          orientation: draft.orientation,
          isDefault: draft.isDefault,
          elements: draft.elements.map((e) => ({
            elementType: e.elementType,
            xmm: e.xmm,
            ymm: e.ymm,
            widthMm: e.widthMm,
            heightMm: e.heightMm,
            rotation: e.rotation,
            zIndex: e.zIndex,
            content: e.content,
            fontName: e.fontName,
            fontSize: e.fontSize,
            barcodeType: e.barcodeType,
            humanReadable: e.humanReadable,
            fitMode: e.fitMode,
            logoId: e.logoId,
            borderThicknessDots: e.borderThicknessDots,
          })),
        }),
      });

      const text = await res.text();
      let body: any = {};
      try {
        body = JSON.parse(text);
      } catch {
        body = { message: text };
      }

      if (!res.ok) throw new Error(body.message || `Failed to save template (${res.status}).`);

      onNotify(`Template '${body.name}' saved successfully.`);
      loadTemplates();
      setSelectedTemplateId(body.id);
      setDraft(body);
    } catch (err: any) {
      onNotify(err.message || "Error saving template.");
    } finally {
      setSaving(false);
    }
  };

  const handleTestPrint = async () => {
    if (!apiToken || !draft.id) {
      onNotify("Save the template first before running a test print.");
      return;
    }
    setTesting(true);
    try {
      const res = await fetch(`${apiUrl}/api/label-templates/${draft.id}/test-print`, {
        method: "POST",
        headers: { Authorization: `Bearer ${apiToken}` },
      });
      const body = await res.json();
      if (!res.ok) throw new Error(body.message || "Test print failed.");

      onNotify(`TSC Test Print #${body.id} sent directly to ${body.printerName}.`);
      onRefreshJobs?.();
    } catch (err: any) {
      onNotify(err.message || "Test print failed.");
    } finally {
      setTesting(false);
    }
  };

  const selectedEl = selectedElementIndex !== null ? draft.elements[selectedElementIndex] : null;

  return (
    <div className="tsc-editor-container" style={{ display: "flex", flexDirection: "column", gap: "16px" }}>
      {/* Header bar */}
      <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", background: "#fff", padding: "14px 18px", borderRadius: "8px", border: "1px solid #e1e4e8" }}>
        <div>
          <span style={{ fontSize: "11px", color: "#666", fontWeight: 700, textTransform: "uppercase" }}>TSC BARCODE ENGINE (TSPL/TSPL-EZ)</span>
          <h2 style={{ margin: "2px 0 0", fontSize: "18px" }}>Barcode Label Template Editor</h2>
          <div style={{ fontSize: "12px", color: activeBarcodePrinter ? "#2e7d32" : "#d32f2f", marginTop: "2px" }}>
            Target Printer: <strong>{activeBarcodePrinter ?? "None selected (Set in Barcode Printing tab)"}</strong>
          </div>
        </div>
        <div style={{ display: "flex", gap: "8px" }}>
          <select
            value={selectedTemplateId ?? ""}
            onChange={(e) => handleSelectTemplate(Number(e.target.value))}
            style={{ padding: "6px 10px", borderRadius: "4px", border: "1px solid #ccc" }}
          >
            {templates.map((t) => (
              <option key={t.id} value={t.id}>
                {t.name} ({t.widthMm}x{t.heightMm}mm) {t.isDefault ? "[DEFAULT]" : ""}
              </option>
            ))}
          </select>
          <button type="button" className="secondary" onClick={handleNewTemplate}>
            + New Template
          </button>
        </div>
      </div>

      {/* Main workspace grid */}
      <div style={{ display: "grid", gridTemplateColumns: "220px 1fr 280px", gap: "16px", alignItems: "start" }}>
        {/* Left: Toolbox & Dimensions */}
        <div style={{ display: "flex", flexDirection: "column", gap: "14px", background: "#fff", padding: "14px", borderRadius: "8px", border: "1px solid #e1e4e8" }}>
          <div>
            <h4 style={{ margin: "0 0 8px", fontSize: "13px", borderBottom: "1px solid #eee", paddingBottom: "4px" }}>Label Dimensions</h4>
            <label style={{ display: "block", fontSize: "11px", marginBottom: "6px" }}>
              Template Name
              <input
                type="text"
                value={draft.name}
                onChange={(e) => setDraft({ ...draft, name: e.target.value })}
                style={{ width: "100%", padding: "4px 6px", boxSizing: "border-box" }}
              />
            </label>
            <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: "6px" }}>
              <label style={{ fontSize: "11px" }}>
                Width (mm)
                <input
                  type="number"
                  min="20"
                  max="200"
                  value={draft.widthMm}
                  onChange={(e) => setDraft({ ...draft, widthMm: Number(e.target.value) })}
                  style={{ width: "100%", padding: "4px" }}
                />
              </label>
              <label style={{ fontSize: "11px" }}>
                Height (mm)
                <input
                  type="number"
                  min="15"
                  max="200"
                  value={draft.heightMm}
                  onChange={(e) => setDraft({ ...draft, heightMm: Number(e.target.value) })}
                  style={{ width: "100%", padding: "4px" }}
                />
              </label>
            </div>
            <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: "6px", marginTop: "6px" }}>
              <label style={{ fontSize: "11px" }}>
                Gap (mm)
                <input
                  type="number"
                  min="0"
                  max="10"
                  step="0.5"
                  value={draft.gapMm}
                  onChange={(e) => setDraft({ ...draft, gapMm: Number(e.target.value) })}
                  style={{ width: "100%", padding: "4px" }}
                />
              </label>
              <label style={{ fontSize: "11px" }}>
                Default?
                <input
                  type="checkbox"
                  checked={draft.isDefault}
                  onChange={(e) => setDraft({ ...draft, isDefault: e.target.checked })}
                  style={{ display: "block", marginTop: "6px" }}
                />
              </label>
            </div>
          </div>

          <div>
            <h4 style={{ margin: "0 0 8px", fontSize: "13px", borderBottom: "1px solid #eee", paddingBottom: "4px" }}>Add Element</h4>
            <div style={{ display: "flex", flexDirection: "column", gap: "6px" }}>
              <button type="button" className="secondary" onClick={() => handleAddElement(1)}>
                + Logo (Graphic)
              </button>
              <button type="button" className="secondary" onClick={() => handleAddElement(2)}>
                + Barcode (Code 128)
              </button>
              <button type="button" className="secondary" onClick={() => handleAddElement(3)}>
                + Text Field
              </button>
              <button type="button" className="secondary" onClick={() => handleAddElement(4)}>
                + Box Border
              </button>
            </div>
          </div>

          <div>
            <h4 style={{ margin: "0 0 8px", fontSize: "13px", borderBottom: "1px solid #eee", paddingBottom: "4px" }}>
              Elements ({draft.elements.length})
            </h4>
            {draft.elements.length === 0 ? (
              <div style={{ fontSize: "11px", color: "#888" }}>No elements added yet.</div>
            ) : (
              <div style={{ display: "flex", flexDirection: "column", gap: "4px", maxHeight: "160px", overflowY: "auto" }}>
                {draft.elements.map((el, idx) => {
                  const isSel = selectedElementIndex === idx;
                  const label = el.elementType === 1 ? `Logo (${el.logoName ?? "Custom"})`
                    : el.elementType === 2 ? `Barcode (${el.barcodeType ?? "128"})`
                    : el.elementType === 3 ? `Text (${el.content || "Empty"})`
                    : `Box (${el.widthMm}x${el.heightMm}mm)`;
                  return (
                    <button
                      key={idx}
                      type="button"
                      onClick={(e) => {
                        e.stopPropagation();
                        setSelectedElementIndex(idx);
                      }}
                      style={{
                        padding: "5px 8px",
                        textAlign: "left",
                        fontSize: "11px",
                        borderRadius: "4px",
                        border: isSel ? "1px solid #2060ff" : "1px solid #e1e4e8",
                        background: isSel ? "#eef4ff" : "#fbfbfb",
                        fontWeight: isSel ? 700 : 400,
                        cursor: "pointer",
                        overflow: "hidden",
                        textOverflow: "ellipsis",
                        whiteSpace: "nowrap",
                      }}
                    >
                      {idx + 1}. {label}
                    </button>
                  );
                })}
              </div>
            )}
          </div>

          <div style={{ marginTop: "10px", borderTop: "1px solid #eee", paddingTop: "12px", display: "flex", flexDirection: "column", gap: "8px" }}>
            <button type="button" className="primary" onClick={handleSaveTemplate} disabled={saving}>
              {saving ? "Saving..." : "Save Template"}
            </button>
            <button type="button" className="secondary" onClick={handleTestPrint} disabled={testing || !draft.id}>
              {testing ? "Printing..." : "⚡ Test Print (TSC RAW)"}
            </button>
          </div>
        </div>

        {/* Center: Canvas preview */}
        <div style={{ background: "#fff", padding: "16px", borderRadius: "8px", border: "1px solid #e1e4e8" }}>
          <LabelCanvas
            template={draft}
            selectedElementIndex={selectedElementIndex}
            onSelectElement={setSelectedElementIndex}
            onUpdateElement={handleUpdateElement}
            apiUrl={apiUrl}
            availableLogos={availableLogos}
          />
        </div>

        {/* Right: Selected Element Properties Inspector */}
        <div style={{ background: "#fff", padding: "14px", borderRadius: "8px", border: "1px solid #e1e4e8" }}>
          <h4 style={{ margin: "0 0 8px", fontSize: "13px", borderBottom: "1px solid #eee", paddingBottom: "4px" }}>
            Element Properties
          </h4>

          {selectedEl !== null && selectedElementIndex !== null ? (
            <div style={{ display: "flex", flexDirection: "column", gap: "10px" }}>
              <div style={{ fontSize: "11px", fontWeight: "bold", color: "#008060" }}>
                Type: {selectedEl.elementType === 1 ? "Logo" : selectedEl.elementType === 2 ? "Barcode" : selectedEl.elementType === 3 ? "Text" : "Box"}
              </div>

              {/* Position and Size (mm) */}
              <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: "6px" }}>
                <label style={{ fontSize: "11px" }}>
                  X (mm)
                  <input
                    type="number"
                    step="0.5"
                    value={selectedEl.xmm}
                    onChange={(e) => handleUpdateElement(selectedElementIndex, { ...selectedEl, xmm: Number(e.target.value) })}
                    style={{ width: "100%", padding: "4px" }}
                  />
                </label>
                <label style={{ fontSize: "11px" }}>
                  Y (mm)
                  <input
                    type="number"
                    step="0.5"
                    value={selectedEl.ymm}
                    onChange={(e) => handleUpdateElement(selectedElementIndex, { ...selectedEl, ymm: Number(e.target.value) })}
                    style={{ width: "100%", padding: "4px" }}
                  />
                </label>
                <label style={{ fontSize: "11px" }}>
                  Width (mm)
                  <input
                    type="number"
                    step="0.5"
                    value={selectedEl.widthMm}
                    onChange={(e) => handleUpdateElement(selectedElementIndex, { ...selectedEl, widthMm: Number(e.target.value) })}
                    style={{ width: "100%", padding: "4px" }}
                  />
                </label>
                <label style={{ fontSize: "11px" }}>
                  Height (mm)
                  <input
                    type="number"
                    step="0.5"
                    value={selectedEl.heightMm}
                    onChange={(e) => handleUpdateElement(selectedElementIndex, { ...selectedEl, heightMm: Number(e.target.value) })}
                    style={{ width: "100%", padding: "4px" }}
                  />
                </label>
              </div>

              {/* Logo specific */}
              {selectedEl.elementType === 1 && (
                <>
                  <label style={{ fontSize: "11px" }}>
                    <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", marginBottom: "3px" }}>
                      <span>Select Uploaded Logo ({availableLogos.length} available)</span>
                      <button
                        type="button"
                        onClick={loadLogos}
                        style={{ fontSize: "10px", padding: "1px 6px", border: "1px solid #ccc", background: "#f0f0f0", borderRadius: "3px", cursor: "pointer" }}
                        title="Reload logos from server"
                      >
                        ↻ Refresh
                      </button>
                    </div>
                    <select
                      value={selectedEl.logoId ?? ""}
                      onChange={(e) => {
                        const val = e.target.value;
                        const lId = val ? Number(val) : null;
                        const match = availableLogos.find((l) => l.id === lId);
                        handleUpdateElement(selectedElementIndex, {
                          ...selectedEl,
                          logoId: lId,
                          logoName: match?.name,
                          content: match?.fileName,
                        });
                      }}
                      style={{ width: "100%", padding: "4px" }}
                    >
                      <option value="">-- Choose Logo --</option>
                      {availableLogos.map((l) => (
                        <option key={l.id} value={l.id}>
                          {l.name} ({l.fileName})
                        </option>
                      ))}
                    </select>
                  </label>

                  {/* Inline Quick Logo Upload */}
                  <div style={{ background: "#f8f9fa", border: "1px dashed #ccc", padding: "8px", borderRadius: "4px" }}>
                    <div style={{ fontSize: "11px", fontWeight: 600, marginBottom: "4px" }}>Upload New Logo</div>
                    <input
                      type="file"
                      accept=".png,.jpg,.jpeg,.svg,.webp"
                      style={{ fontSize: "11px", width: "100%" }}
                      onChange={async (e) => {
                        const file = e.target.files?.[0];
                        if (!file) return;
                        const logoName = file.name.replace(/\.[^/.]+$/, "");
                        const form = new FormData();
                        form.append("name", logoName);
                        form.append("file", file);
                        try {
                          const res = await fetch(`${apiUrl}/api/labels/logos`, {
                            method: "POST",
                            headers: { Authorization: `Bearer ${apiToken}` },
                            body: form,
                          });
                          const created = await res.json();
                          if (!res.ok) throw new Error(created.message || "Upload failed");
                          onNotify(`Logo '${created.name}' uploaded successfully.`);
                          setAvailableLogos((prev) => [...prev, created]);
                          handleUpdateElement(selectedElementIndex, {
                            ...selectedEl,
                            logoId: created.id,
                            logoName: created.name,
                            content: created.fileName,
                          });
                        } catch (err: any) {
                          onNotify(err.message || "Failed to upload logo.");
                        }
                      }}
                    />
                    <div style={{ fontSize: "10px", color: "#666", marginTop: "3px" }}>
                      Supports PNG, JPG, SVG, WEBP.
                    </div>
                  </div>

                  <label style={{ fontSize: "11px" }}>
                    Logo Fit Mode
                    <select
                      value={selectedEl.fitMode}
                      onChange={(e) => handleUpdateElement(selectedElementIndex, { ...selectedEl, fitMode: Number(e.target.value) as any })}
                      style={{ width: "100%", padding: "4px" }}
                    >
                      <option value={1}>Contain (Keep aspect, no crop)</option>
                      <option value={2}>Cover (Fill area, crop excess)</option>
                      <option value={3}>Stretch (Distort to bounds)</option>
                    </select>
                  </label>
                </>
              )}

              {/* Text specific */}
              {selectedEl.elementType === 3 && (
                <>
                  <label style={{ fontSize: "11px" }}>
                    Content / Placeholder
                    <input
                      type="text"
                      value={selectedEl.content ?? ""}
                      onChange={(e) => handleUpdateElement(selectedElementIndex, { ...selectedEl, content: e.target.value })}
                      placeholder="e.g. {ItemName} or static text"
                      style={{ width: "100%", padding: "4px" }}
                    />
                  </label>
                  <label style={{ fontSize: "11px" }}>
                    Font Size
                    <input
                      type="number"
                      min="8"
                      max="48"
                      value={selectedEl.fontSize}
                      onChange={(e) => handleUpdateElement(selectedElementIndex, { ...selectedEl, fontSize: Number(e.target.value) })}
                      style={{ width: "100%", padding: "4px" }}
                    />
                  </label>
                </>
              )}

              {/* Barcode specific */}
              {selectedEl.elementType === 2 && (
                <>
                  <label style={{ fontSize: "11px" }}>
                    Barcode Placeholder
                    <input
                      type="text"
                      value={selectedEl.content ?? ""}
                      onChange={(e) => handleUpdateElement(selectedElementIndex, { ...selectedEl, content: e.target.value })}
                      placeholder="{Barcode}"
                      style={{ width: "100%", padding: "4px" }}
                    />
                  </label>
                  <label style={{ fontSize: "11px", display: "flex", alignItems: "center", gap: "6px" }}>
                    <input
                      type="checkbox"
                      checked={selectedEl.humanReadable}
                      onChange={(e) => handleUpdateElement(selectedElementIndex, { ...selectedEl, humanReadable: e.target.checked })}
                    />
                    Show Human-Readable Digits
                  </label>
                </>
              )}

              <button
                type="button"
                className="secondary"
                style={{ color: "#d00", marginTop: "12px" }}
                onClick={() => handleDeleteElement(selectedElementIndex)}
              >
                Delete Element
              </button>
            </div>
          ) : (
            <p style={{ fontSize: "12px", color: "#888" }}>Click on any element in the canvas to inspect and edit its physical properties.</p>
          )}
        </div>
      </div>
    </div>
  );
};
