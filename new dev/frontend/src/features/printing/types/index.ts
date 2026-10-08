export type TemplateElementType = 1 | 2 | 3 | 4; // 1: Logo, 2: Barcode, 3: Text, 4: Box
export type LogoFitMode = 1 | 2 | 3; // 1: Contain, 2: Cover, 3: Stretch

export interface LabelTemplateElement {
  id?: number;
  elementType: TemplateElementType;
  xmm: number;
  ymm: number;
  widthMm: number;
  heightMm: number;
  rotation: number;
  zIndex: number;
  content?: string;
  fontName?: string;
  fontSize: number;
  barcodeType?: string;
  humanReadable: boolean;
  fitMode: LogoFitMode;
  logoId?: number | null;
  logoName?: string | null;
  borderThicknessDots: number;
}

export interface LabelTemplate {
  id: number;
  name: string;
  printerType: number; // 1: Laser, 2: TSC
  widthMm: number;
  heightMm: number;
  gapMm: number;
  orientation: number;
  isDefault: boolean;
  elements: LabelTemplateElement[];
  createdAt?: string;
  updatedAt?: string;
}

export interface AvailablePrinter {
  name: string;
  displayName: string;
  status: string;
  isDefault: boolean;
}

export interface PrinterConfigItem {
  id: number;
  category: "RegularDocument" | "Barcode";
  printerName: string;
  isActive: boolean;
  updatedAt?: string;
  updatedBy?: string;
  mode: number; // 1 = Laser, 2 = TSC
  model: string;
  dpi: number;
  activeTemplateId?: number | null;
  activeTemplateName?: string | null;
}

export interface PrinterConfigurationsSummary {
  regularDocumentPrinter: PrinterConfigItem | null;
  barcodePrinter: PrinterConfigItem | null;
}

export interface PrintJobRow {
  id: number;
  category: string;
  printerName: string;
  documentName: string;
  documentReference?: string;
  copies: number;
  status: string;
  requestedBy?: string;
  createdAt: string;
  startedAt?: string;
  completedAt?: string;
  errorMessage?: string;
}
