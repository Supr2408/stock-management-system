import React, { useState } from "react";
import type { LabelTemplate, LabelTemplateElement } from "../types";

interface Props {
  template: LabelTemplate;
  selectedElementIndex: number | null;
  onSelectElement: (index: number | null) => void;
  onUpdateElement: (index: number, updated: LabelTemplateElement) => void;
  apiUrl: string;
  availableLogos?: { id: number; name: string; fileName: string; relativePath?: string }[];
}

export const LabelCanvas: React.FC<Props> = ({
  template,
  selectedElementIndex,
  onSelectElement,
  onUpdateElement,
  apiUrl,
  availableLogos = [],
}) => {
  // Scaling: calculate scale to fit a preview container of max 480px width
  const maxCanvasWidthPx = 480;
  const scale = maxCanvasWidthPx / Math.max(1, template.widthMm);
  const canvasWidthPx = template.widthMm * scale;
  const canvasHeightPx = template.heightMm * scale;

  const [dragState, setDragState] = useState<{
    index: number;
    startX: number;
    startY: number;
    origXmm: number;
    origYmm: number;
    hasMoved: boolean;
  } | null>(null);

  const [brokenImages, setBrokenImages] = useState<Record<number, boolean>>({});

  const resolveLogoSrc = (el: LabelTemplateElement): string | null => {
    let fileOrPath = el.content;
    if (!fileOrPath && el.logoId && availableLogos.length > 0) {
      const match = availableLogos.find((l) => l.id === el.logoId);
      if (match) fileOrPath = match.fileName || match.relativePath;
    }
    if (!fileOrPath) return null;

    if (fileOrPath.startsWith("http://") || fileOrPath.startsWith("https://") || fileOrPath.startsWith("data:")) {
      return fileOrPath;
    }

    const clean = fileOrPath.replace(/^\/+/, "");
    const base = (apiUrl || "").replace(/\/+$/, "");

    if (clean.startsWith("barcode/logos/")) {
      return `${base}/${clean}`;
    }
    if (clean.startsWith("logos/")) {
      return `${base}/barcode/${clean}`;
    }
    return `${base}/barcode/logos/${clean}`;
  };

  const handleMouseDown = (e: React.MouseEvent, index: number) => {
    e.stopPropagation();
    onSelectElement(index);
    const el = template.elements[index];
    setDragState({
      index,
      startX: e.clientX,
      startY: e.clientY,
      origXmm: el.xmm,
      origYmm: el.ymm,
      hasMoved: false,
    });
  };

  const handleElementClick = (e: React.MouseEvent, index: number) => {
    e.stopPropagation();
    onSelectElement(index);
  };

  const handleMouseMove = (e: React.MouseEvent) => {
    if (!dragState) return;
    const dxPx = e.clientX - dragState.startX;
    const dyPx = e.clientY - dragState.startY;

    // Small jitter threshold: do not move until at least 4 pixels dragged
    if (!dragState.hasMoved && Math.hypot(dxPx, dyPx) < 4) {
      return;
    }

    if (!dragState.hasMoved) {
      dragState.hasMoved = true;
    }

    const dxMm = dxPx / scale;
    const dyMm = dyPx / scale;

    const el = template.elements[dragState.index];
    const newX = Math.max(0, Math.min(template.widthMm - el.widthMm, Math.round((dragState.origXmm + dxMm) * 2) / 2));
    const newY = Math.max(0, Math.min(template.heightMm - el.heightMm, Math.round((dragState.origYmm + dyMm) * 2) / 2));

    if (newX !== el.xmm || newY !== el.ymm) {
      onUpdateElement(dragState.index, { ...el, xmm: newX, ymm: newY });
    }
  };

  const handleMouseUp = () => {
    setDragState(null);
  };

  const handleCanvasBackgroundClick = (e: React.MouseEvent) => {
    // Only deselect if clicked directly on background and not dragging
    if (e.target === e.currentTarget && !dragState?.hasMoved) {
      onSelectElement(null);
    }
  };

  return (
    <div
      className="label-canvas-outer"
      style={{
        display: "flex",
        flexDirection: "column",
        alignItems: "center",
        padding: "16px",
        background: "#f0f2f5",
        borderRadius: "8px",
        userSelect: "none",
      }}
      onMouseMove={handleMouseMove}
      onMouseUp={handleMouseUp}
      onMouseLeave={handleMouseUp}
    >
      <div style={{ marginBottom: "8px", fontSize: "12px", color: "#666", fontWeight: 600 }}>
        Physical Aspect: {template.widthMm}mm × {template.heightMm}mm (1mm = 8 dots @ 203 DPI)
      </div>

      <div
        className="label-canvas"
        onClick={handleCanvasBackgroundClick}
        style={{
          width: `${canvasWidthPx}px`,
          height: `${canvasHeightPx}px`,
          background: "#ffffff",
          border: "2px dashed #008060",
          boxShadow: "0 4px 12px rgba(0,0,0,0.1)",
          position: "relative",
          overflow: "hidden",
        }}
      >
        {template.elements.map((el, idx) => {
          const isSelected = selectedElementIndex === idx;
          const left = el.xmm * scale;
          const top = el.ymm * scale;
          const width = el.widthMm * scale;
          const height = el.heightMm * scale;

          const exceedsBounds = el.xmm + el.widthMm > template.widthMm + 0.1 || el.ymm + el.heightMm > template.heightMm + 0.1;
          const logoSrc = el.elementType === 1 ? resolveLogoSrc(el) : null;

          return (
            <div
              key={idx}
              onMouseDown={(e) => handleMouseDown(e, idx)}
              onClick={(e) => handleElementClick(e, idx)}
              style={{
                position: "absolute",
                left: `${left}px`,
                top: `${top}px`,
                width: `${width}px`,
                height: `${height}px`,
                border: isSelected ? "2px solid #2060ff" : exceedsBounds ? "2px solid #ff4444" : "1px solid rgba(0,0,0,0.2)",
                boxSizing: "border-box",
                cursor: "move",
                zIndex: el.zIndex || idx + 1,
                background: el.elementType === 4 ? "transparent" : "rgba(255,255,255,0.92)",
                display: "flex",
                alignItems: "center",
                justifyContent: "center",
                overflow: "hidden",
                transform: el.rotation ? `rotate(${el.rotation}deg)` : undefined,
                boxShadow: isSelected ? "0 0 8px rgba(32, 96, 255, 0.4)" : "none",
              }}
              title={`${el.elementType === 1 ? "Logo" : el.elementType === 2 ? "Barcode" : el.elementType === 3 ? "Text" : "Box"}: ${el.widthMm}x${el.heightMm}mm at (${el.xmm},${el.ymm})`}
            >
              {el.elementType === 1 && (
                <div style={{ width: "100%", height: "100%", display: "flex", alignItems: "center", justifyContent: "center" }}>
                  {logoSrc && !brokenImages[idx] ? (
                    <img
                      src={logoSrc}
                      alt={el.logoName ?? "Logo"}
                      onError={() => setBrokenImages((prev) => ({ ...prev, [idx]: true }))}
                      style={{
                        width: "100%",
                        height: "100%",
                        objectFit: el.fitMode === 2 ? "cover" : el.fitMode === 3 ? "fill" : "contain",
                        pointerEvents: "none",
                      }}
                    />
                  ) : (
                    <div style={{ fontSize: "11px", fontWeight: "bold", color: "#666", textAlign: "center", padding: "2px" }}>
                      [{el.logoName || "LOGO"}]
                    </div>
                  )}
                </div>
              )}

              {el.elementType === 2 && (
                <div style={{ width: "100%", height: "100%", display: "flex", flexDirection: "column", alignItems: "center", justifyContent: "center", padding: "2px" }}>
                  <div
                    style={{
                      width: "90%",
                      height: "65%",
                      background: "repeating-linear-gradient(90deg, #000, #000 2px, #fff 2px, #fff 5px, #000 5px, #000 8px, #fff 8px, #fff 10px)",
                    }}
                  />
                  {el.humanReadable && (
                    <span style={{ fontSize: "9px", fontFamily: "monospace", marginTop: "2px", letterSpacing: "1px" }}>
                      {el.content || "143081026000001"}
                    </span>
                  )}
                </div>
              )}

              {el.elementType === 3 && (
                <div
                  style={{
                    padding: "2px 4px",
                    width: "100%",
                    height: "100%",
                    display: "flex",
                    alignItems: "center",
                    fontSize: `${Math.max(9, (el.fontSize || 12) * (scale / 4))}px`,
                    fontWeight: 600,
                    wordBreak: "break-word",
                  }}
                >
                  {el.content || "Sample Text"}
                </div>
              )}

              {el.elementType === 4 && (
                <div
                  style={{
                    width: "100%",
                    height: "100%",
                    border: `${Math.max(1, el.borderThicknessDots || 2)}px solid #000`,
                    boxSizing: "border-box",
                  }}
                />
              )}
            </div>
          );
        })}
      </div>
    </div>
  );
};
