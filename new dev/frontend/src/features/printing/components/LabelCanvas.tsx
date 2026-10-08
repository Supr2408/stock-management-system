import React, { useState } from "react";
import type { LabelTemplate, LabelTemplateElement } from "../types";

interface Props {
  template: LabelTemplate;
  selectedElementIndex: number | null;
  onSelectElement: (index: number | null) => void;
  onUpdateElement: (index: number, updated: LabelTemplateElement) => void;
  apiUrl: string;
}

export const LabelCanvas: React.FC<Props> = ({
  template,
  selectedElementIndex,
  onSelectElement,
  onUpdateElement,
  apiUrl,
}) => {
  // Scaling: calculate scale to fit a preview container of max 520px width
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
  } | null>(null);

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
    // Only deselect if clicked directly on background
    if (e.target === e.currentTarget) {
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
      onClick={handleCanvasBackgroundClick}
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
                background: el.elementType === 4 ? "transparent" : "rgba(255,255,255,0.85)",
                display: "flex",
                alignItems: "center",
                justifyContent: "center",
                overflow: "hidden",
                transform: el.rotation ? `rotate(${el.rotation}deg)` : undefined,
              }}
              title={`${el.elementType === 1 ? "Logo" : el.elementType === 2 ? "Barcode" : el.elementType === 3 ? "Text" : "Box"}: ${el.widthMm}x${el.heightMm}mm at (${el.xmm},${el.ymm})`}
            >
              {el.elementType === 1 && (
                <div style={{ width: "100%", height: "100%", display: "flex", alignItems: "center", justifyContent: "center" }}>
                  {el.content ? (
                    <img
                      src={`${apiUrl}/barcode/logos/${el.content}`}
                      alt={el.logoName ?? "Logo"}
                      style={{
                        width: "100%",
                        height: "100%",
                        objectFit: el.fitMode === 2 ? "cover" : el.fitMode === 3 ? "fill" : "contain",
                        pointerEvents: "none",
                      }}
                    />
                  ) : (
                    <div style={{ fontSize: "11px", fontWeight: "bold", color: "#888" }}>[LOGO]</div>
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
