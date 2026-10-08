import { useEffect, useMemo, useRef, useState } from "react";
import type { ChangeEvent, FormEvent } from "react";
import { PDFDocument, StandardFonts, rgb } from "pdf-lib";
import * as XLSX from "xlsx";
import "./App.css";
import { TscLabelEditor } from "./features/printing/components/TscLabelEditor";
import type { LabelTemplate } from "./features/printing/types";

type Department = { id: number; name: string };
type Customer = { id: number; legacyId?: number; name: string; address1: string; address2: string; city: string; pincode: string; state: string; country: string };
type Item = {
  id: number;
  code: number;
  name: string;
  description: string;
  packing: number;
  departmentId: number;
  weight: number;
};
type MasterTab = "departments" | "items";
type LabelTab = "print" | "duplicate";
type DispatchTab = "manage" | "history";
type ReportTab = "batchNo" | "customerSoDetails" | "stock" | "salesOrder" | "itemDetail" | "productWiseSummary" | "batchWiseSummary" | "stockSummary" | "boxLabel" | "salesOrderSummary" | "todaysPrintDetails";
type ImportIssue = { row: number; message: string; code?: string };
type ItemDuplicateComparison = {
  row: number;
  existing: ApiItem & { departmentName: string };
  uploaded: {
    code?: number;
    name?: string;
    description?: string;
    packagingPerBox?: number;
    grossWeightKg?: number;
    department?: string;
    departmentId?: number;
  };
};
type CustomerDuplicateComparison = {
  row: number;
  existing: ApiCustomer;
  uploaded: {
    legacyId?: number;
    name?: string;
    address1?: string;
    address2?: string;
    city?: string;
    pincode?: string;
    state?: string;
    country?: string;
  };
};
type ApiItem = {
  id: number;
  code: number;
  name: string;
  description?: string;
  packagingPerBox: number;
  grossWeightKg: number;
  departmentId: number;
};
type ApiCustomer = Omit<Customer, "address1" | "address2" | "city" | "pincode" | "state" | "country"> & { address1?: string; address2?: string; city?: string; pincode?: string; state?: string; country?: string };
type LabelLogo = { id: number; name: string; fileName: string; relativePath: string; contentType: string };
type PendingProductionLabel = { id: number; itemId: number; itemCode: number; itemName: string; departmentName: string; barcodeValue: string; manufactureDate: string; serialNumber: number };
type DispatchResult = { dispatchId: number; dispatched: number; salesOrderNumber: string; invoiceNumber: string; customer: { name: string; address1?: string; address2?: string; city?: string; pincode?: string; state?: string; country?: string }; dispatchDate: string; labels: { barcodeValue: string; itemCode: number; itemName: string; batchNumber: string; piecesPerBox: number; grossWeightKg: number }[] };
type DispatchHistoryRow = { dispatchId: number; salesOrderNumber: string; invoiceNumber: string; customer: DispatchResult["customer"]; dispatchDate: string; sourceFileName: string; labelCount: number; labels: DispatchResult["labels"] };
type DailyPrintRow = { itemCode: number; itemName: string; departmentName: string; manufactureDate: string; totalPrint: number; startingBarcode: string; endingBarcode: string };
type StockSummaryRow = { itemCode: number; itemName: string; departmentName: string; totalStockQuantity: number };
type ItemDetailRow = { barcodeValue: string; itemCode: number; itemName: string; departmentName: string; manufactureDate: string; serialNumber: number; status: string; batchNumber?: string; customerName?: string; salesOrderNumber?: string; invoiceNumber?: string; dispatchDate?: string };
type BatchRow = { batchNumber: string; barcodeValue: string; itemName: string; salesOrderNumber?: string; dispatchDate?: string; customerName?: string };
type CustomerReport = { customer: DispatchResult["customer"]; rows: { salesOrderNumber: string; invoiceNumber: string; dispatchDate: string; batchNumber: string; itemName: string; quantity: number }[] };
type InventorySummaryRow = { itemName: string; departmentName: string; customerName: string; batchNumber: string; totalBoxQuantity: number };
type SalesOrderSummaryRow = { salesOrderNumber: string; itemName: string; quantity: number };
type BoxDetailRow = { itemName: string; batchNumber: string; barcodeValue: string; stockStatus: string; salesOrderNumber?: string; customerName: string };
type ReportPrintDocument = { title: string; subtitle?: string; address?: string[]; headers: string[]; rows: string[][] };
type AvailablePrinter = { name: string; displayName: string; status: string; isDefault: boolean };
type UserAccount = {
  id: number;
  username: string;
  role: string;
  departmentId: number | null;
  departmentName: string | null;
  isActive: boolean;
  createdAt: string;
  temporaryDevPassword?: string;
};

type PrinterConfigItem = {
  id: number;
  category: "RegularDocument" | "Barcode";
  printerName: string;
  isActive: boolean;
  updatedAt?: string;
  updatedBy?: string;
  mode?: number;
  model?: string;
  dpi?: number;
  activeTemplateId?: number | null;
  activeTemplateName?: string | null;
  paperSize?: string;
  paperWidthMm?: number;
  paperHeightMm?: number;
  marginLeftMm?: number;
  marginRightMm?: number;
  marginTopMm?: number;
  marginBottomMm?: number;
  horizontalGapMm?: number;
  verticalGapMm?: number;
};
type PrinterConfigurationsSummary = { regularDocumentPrinter: PrinterConfigItem | null; barcodePrinter: PrinterConfigItem | null };
type PrintJobRow = { id: number; category: string; printerName: string; documentName: string; documentReference?: string; copies: number; status: string; requestedBy?: string; createdAt: string; startedAt?: string; completedAt?: string; errorMessage?: string };

const menu = [
  "Master",
  "Customer",
  "Label",
  "Production",
  "Dispatch",
  "User",
  "Reports",
  "More",
  "Help",
];
const apiUrl = import.meta.env.VITE_API_URL ?? "http://localhost:5254";
const indianStates = [
  "Andhra Pradesh",
  "Arunachal Pradesh",
  "Assam",
  "Bihar",
  "Chhattisgarh",
  "Goa",
  "Gujarat",
  "Haryana",
  "Himachal Pradesh",
  "Jharkhand",
  "Karnataka",
  "Kerala",
  "Madhya Pradesh",
  "Maharashtra",
  "Manipur",
  "Meghalaya",
  "Mizoram",
  "Nagaland",
  "Odisha",
  "Punjab",
  "Rajasthan",
  "Sikkim",
  "Tamil Nadu",
  "Telangana",
  "Tripura",
  "Uttar Pradesh",
  "Uttarakhand",
  "West Bengal",
  "Andaman and Nicobar Islands",
  "Chandigarh",
  "Dadra and Nagar Haveli and Daman and Diu",
  "Delhi",
  "Jammu and Kashmir",
  "Ladakh",
  "Lakshadweep",
  "Puducherry",
  "Other",
];
const countries = [
  "India",
  "Bangladesh",
  "Bhutan",
  "Nepal",
  "Sri Lanka",
  "United Arab Emirates",
  "United Kingdom",
  "United States",
  "Other",
];
const commonCities = [
  "Ahmedabad",
  "Aurangabad",
  "Bengaluru",
  "Bhilai",
  "Bokaro",
  "Chennai",
  "Coimbatore",
  "Delhi",
  "Erode",
  "Gaya",
  "Hubli",
  "Indore",
  "Kanpur",
  "Kochi",
  "Mumbai",
  "Pondicherry",
  "Surat",
  "Thane",
  "Thrissur",
  "Vapi",
];
const initialDepartments: Department[] = [
  { id: 1, name: "AFC" },
  { id: 2, name: "AFR" },
  { id: 3, name: "PLASCON" },
];
const initialItems: Item[] = [
  {
    id: 1,
    code: 101,
    name: "100 ML REGULAR",
    description: "FOIL 37X435 MM, PACKING 5000 PCS",
    packing: 5000,
    departmentId: 1,
    weight: 7.2,
  },
  {
    id: 2,
    code: 102,
    name: "110 ML REGULAR",
    description: "FOIL 37X340 MM, PACKING 5000 PCS",
    packing: 5000,
    departmentId: 1,
    weight: 7.3,
  },
  {
    id: 3,
    code: 103,
    name: "120 ML REGULAR",
    description: "FOIL 37X539 MM, PACKING 5000 PCS",
    packing: 5000,
    departmentId: 1,
    weight: 9.3,
  },
];

function fromApiItem(item: ApiItem): Item {
  return {
    id: item.id,
    code: item.code,
    name: item.name,
    description: item.description ?? "",
    packing: item.packagingPerBox,
    departmentId: item.departmentId,
    weight: item.grossWeightKg,
  };
}

function fromApiCustomer(customer: ApiCustomer): Customer {
  return { id: customer.id, legacyId: customer.legacyId, name: customer.name, address1: customer.address1 ?? "", address2: customer.address2 ?? "", city: customer.city ?? "", pincode: customer.pincode ?? "", state: customer.state ?? "", country: customer.country ?? "" };
}

function App() {
  const [loggedIn, setLoggedIn] = useState(false);
  const [active, setActive] = useState("Master");
  const [masterTab, setMasterTab] = useState<MasterTab>("departments");
  const [labelTab, setLabelTab] = useState<LabelTab>("print");
  const [reportTab, setReportTab] = useState<ReportTab>("batchNo");
  const [reportDate, setReportDate] = useState(new Date().toISOString().slice(0, 10));
  const [reportDepartmentId, setReportDepartmentId] = useState("");
  const [reportItemId, setReportItemId] = useState("");
  const [reportBarcode, setReportBarcode] = useState("");
  const [reportBatch, setReportBatch] = useState("");
  const [batchOptions, setBatchOptions] = useState<string[]>([]);
  const [reportItemSearch, setReportItemSearch] = useState("");
  const [isReportItemOpen, setIsReportItemOpen] = useState(false);
  const [isReportBatchOpen, setIsReportBatchOpen] = useState(false);
  const [isStockBatchOpen, setIsStockBatchOpen] = useState(false);
  const [reportCustomerId, setReportCustomerId] = useState("");
  const [reportSalesOrder, setReportSalesOrder] = useState("");
  const [reportFromBarcode, setReportFromBarcode] = useState("");
  const [reportToBarcode, setReportToBarcode] = useState("");
  const [dailyPrintRows, setDailyPrintRows] = useState<DailyPrintRow[]>([]);
  const [stockReportRows, setStockReportRows] = useState<StockSummaryRow[]>([]);
  const [itemDetailReport, setItemDetailReport] = useState<ItemDetailRow | null>(null);
  const [batchReportRows, setBatchReportRows] = useState<BatchRow[]>([]);
  const [customerReport, setCustomerReport] = useState<CustomerReport | null>(null);
  const [inventorySummaryRows, setInventorySummaryRows] = useState<InventorySummaryRow[]>([]);
  const [salesOrderSummaryRows, setSalesOrderSummaryRows] = useState<SalesOrderSummaryRow[]>([]);
  const [boxDetailRows, setBoxDetailRows] = useState<BoxDetailRow[]>([]);
  const [reportPrintDocument, setReportPrintDocument] = useState<ReportPrintDocument | null>(null);
  const [moreTab, setMoreTab] = useState<"printing">("printing");
  const [printingSubTab, setPrintingSubTab] = useState<"regular" | "barcode">("regular");
  const [barcodeMode, setBarcodeMode] = useState<"laser" | "tsc">("laser");
  const [tscSubTab, setTscSubTab] = useState<"settings" | "editor">("settings");
  const [availablePrinters, setAvailablePrinters] = useState<AvailablePrinter[]>([]);
  const [printerConfig, setPrinterConfig] = useState<PrinterConfigurationsSummary>({ regularDocumentPrinter: null, barcodePrinter: null });
  const [selectedRegularPrinter, setSelectedRegularPrinter] = useState<string>("");
  const [selectedBarcodePrinter, setSelectedBarcodePrinter] = useState<string>("");
  const [loadingPrinters, setLoadingPrinters] = useState(false);
  const [savingPrinter, setSavingPrinter] = useState(false);
  const [testingPrinter, setTestingPrinter] = useState(false);
  const [recentPrintJobs, setRecentPrintJobs] = useState<PrintJobRow[]>([]);
  const [userName, setUserName] = useState("");
  const [password, setPassword] = useState("");
  const [loginError, setLoginError] = useState("");
  const [apiToken, setApiToken] = useState("");
  const [currentUserRole, setCurrentUserRole] = useState<string>("Admin");
  const [currentUserDeptId, setCurrentUserDeptId] = useState<number | null>(null);
  const [currentUserDeptName, setCurrentUserDeptName] = useState<string | null>(null);
  const [currentUsername, setCurrentUsername] = useState<string>("");
  const [userAccounts, setUserAccounts] = useState<UserAccount[]>([]);
  const [loadingUsers, setLoadingUsers] = useState(false);
  const [laserPaperSize, setLaserPaperSize] = useState("A4");
  const [laserPaperWidthMm, setLaserPaperWidthMm] = useState(210);
  const [laserPaperHeightMm, setLaserPaperHeightMm] = useState(297);
  const [laserMarginLeftMm, setLaserMarginLeftMm] = useState(10);
  const [laserMarginRightMm, setLaserMarginRightMm] = useState(10);
  const [laserMarginTopMm, setLaserMarginTopMm] = useState(10);
  const [laserMarginBottomMm, setLaserMarginBottomMm] = useState(10);
  const [laserGapXMm, setLaserGapXMm] = useState(2);
  const [laserGapYMm, setLaserGapYMm] = useState(2);
  const [labelTemplates, setLabelTemplates] = useState<LabelTemplate[]>([]);
  const [selectedBarcodeTemplateId, setSelectedBarcodeTemplateId] = useState<number | null>(null);
  const [departments, setDepartments] = useState(initialDepartments);
  const [items, setItems] = useState(initialItems);
  const [customers, setCustomers] = useState<Customer[]>([]);
  const [departmentDraft, setDepartmentDraft] = useState({ id: 0, name: "" });
  const [itemDraft, setItemDraft] = useState({
    id: 0,
    name: "",
    description: "",
    packing: "",
    departmentId: "1",
    weight: "",
  });
  const [departmentSearch, setDepartmentSearch] = useState("");
  const [itemSearch, setItemSearch] = useState({ name: "", departmentId: "" });
  const [customerSearch, setCustomerSearch] = useState("");
  const [customerDraft, setCustomerDraft] = useState<Customer>({ id: 0, name: "", address1: "", address2: "", city: "", pincode: "", state: "", country: "" });
  const [labelDraft, setLabelDraft] = useState({ departmentId: "", itemId: "", manufactureDate: new Date().toISOString().slice(0, 10), quantity: "1", templateId: "" });
  const [labelLogos, setLabelLogos] = useState<LabelLogo[]>([]);
  const [labelRange, setLabelRange] = useState({ from: "", to: "" });
  const [logoUpload, setLogoUpload] = useState({ name: "", file: null as File | null });
  const [isLogoDialogOpen, setIsLogoDialogOpen] = useState(false);
  const [labelItemSearch, setLabelItemSearch] = useState("");
  const [labelItemPicker, setLabelItemPicker] = useState("");
  const [isItemPickerOpen, setIsItemPickerOpen] = useState(false);
  const [productionDepartmentId, setProductionDepartmentId] = useState("");
  const [productionSearch, setProductionSearch] = useState("");
  const [isProductionMonthFilterOn, setIsProductionMonthFilterOn] = useState(false);
  const [productionMonth, setProductionMonth] = useState(new Date().toISOString().slice(0, 7));
  const [pendingProduction, setPendingProduction] = useState<PendingProductionLabel[]>([]);
  const [productionTotal, setProductionTotal] = useState(0);
  const [stockDraft, setStockDraft] = useState({ batchNumber: "", fromBarcode: "", toBarcode: "" });
  const [dispatchTab, setDispatchTab] = useState<DispatchTab>("manage");
  const [dispatchDraft, setDispatchDraft] = useState({ customerId: "", customerSearch: "", salesOrderNumber: "", invoiceNumber: "", dispatchDate: new Date().toISOString().slice(0, 10), file: null as File | null });
  const [isDispatchCustomerOpen, setIsDispatchCustomerOpen] = useState(false);
  const [dispatchResult, setDispatchResult] = useState<DispatchResult | null>(null);
  const [oldDispatchSearch, setOldDispatchSearch] = useState({ salesOrderNumber: "", invoiceNumber: "", customer: "", dispatchDate: "", barcode: "" });
  const [oldDispatches, setOldDispatches] = useState<DispatchHistoryRow[]>([]);
  const [oldDispatchTotal, setOldDispatchTotal] = useState(0);
  const [selectedOldDispatch, setSelectedOldDispatch] = useState<DispatchHistoryRow | null>(null);
  const [isPackingPreviewOpen, setIsPackingPreviewOpen] = useState(false);
  const [dispatchPreviewKind, setDispatchPreviewKind] = useState<"salesOrder" | "packing">("salesOrder");
  const [message, setMessage] = useState("");
  const [importIssues, setImportIssues] = useState<ImportIssue[]>([]);
  const [duplicateComparisons, setDuplicateComparisons] = useState<ItemDuplicateComparison[]>([]);
  const [customerDuplicateComparisons, setCustomerDuplicateComparisons] = useState<CustomerDuplicateComparison[]>([]);
  const importRef = useRef<HTMLInputElement>(null);
  const customerImportRef = useRef<HTMLInputElement>(null);

  const nextCode = useMemo(
    () => Math.max(100, ...items.map((item) => item.code)) + 1,
    [items],
  );
  const filteredDepartments = departments.filter((department) =>
    department.name.toLowerCase().includes(departmentSearch.toLowerCase()),
  );
  const filteredItems = items.filter(
    (item) =>
      item.name.toLowerCase().includes(itemSearch.name.toLowerCase()) &&
      (!itemSearch.departmentId ||
        String(item.departmentId) === itemSearch.departmentId),
  );
  const filteredCustomers = customers.filter((customer) => customer.name.toLowerCase().includes(customerSearch.toLowerCase()));
  const cityOptions = useMemo(() => Array.from(new Set([...commonCities, ...customers.map((customer) => customer.city).filter(Boolean)])).sort((left, right) => left.localeCompare(right)), [customers]);

  const roleMenu = useMemo(() => {
    if (currentUserRole === "Production") {
      return ["Production", "Reports"];
    }
    if (currentUserRole === "QC") {
      return ["Production", "Label", "Dispatch", "Reports"];
    }
    return menu;
  }, [currentUserRole]);

  const activeLaserTemplate = useMemo(() => {
    if (selectedBarcodeTemplateId) {
      const found = labelTemplates.find((t) => t.id === selectedBarcodeTemplateId);
      if (found) return found;
    }
    if (printerConfig.barcodePrinter?.activeTemplateId) {
      const found = labelTemplates.find((t) => t.id === printerConfig.barcodePrinter.activeTemplateId);
      if (found) return found;
    }
    return labelTemplates.find((t) => t.isDefault) ?? labelTemplates[0] ?? null;
  }, [selectedBarcodeTemplateId, printerConfig.barcodePrinter, labelTemplates]);

  const laserCalculations = useMemo(() => {
    const usableW = Math.max(0, laserPaperWidthMm - laserMarginLeftMm - laserMarginRightMm);
    const usableH = Math.max(0, laserPaperHeightMm - laserMarginTopMm - laserMarginBottomMm);
    const labelW = activeLaserTemplate ? Math.max(1, activeLaserTemplate.widthMm) : 50;
    const labelH = activeLaserTemplate ? Math.max(1, activeLaserTemplate.heightMm) : 30;
    let cols = Math.max(0, Math.floor((usableW + laserGapXMm) / (labelW + laserGapXMm)));
    if (cols < 2 && usableW >= labelW * 2) {
      cols = 2;
    }
    const rows = Math.max(0, Math.floor((usableH + laserGapYMm) / (labelH + laserGapYMm)));
    return {
      usableW: Number(usableW.toFixed(1)),
      usableH: Number(usableH.toFixed(1)),
      cols,
      rows,
      perPage: cols * rows,
      labelW,
      labelH,
      templateName: activeLaserTemplate?.name ?? "Default"
    };
  }, [laserPaperWidthMm, laserPaperHeightMm, laserMarginLeftMm, laserMarginRightMm, laserMarginTopMm, laserMarginBottomMm, laserGapXMm, laserGapYMm, activeLaserTemplate]);

  const departmentLabelItems = items.filter((item) => !labelDraft.departmentId || String(item.departmentId) === labelDraft.departmentId);
  const labelItems = departmentLabelItems.filter((item) => {
    const query = labelItemSearch.trim().toLowerCase();
    return !query || `${item.code} ${item.name} ${item.description}`.toLowerCase().includes(query);
  });
  const labelPickerItems = departmentLabelItems.filter((item) => {
    const query = labelItemPicker.trim().toLowerCase();
    return !query || `${item.code} ${item.name} ${item.description}`.toLowerCase().includes(query);
  });
  const productionRangeCount = useMemo(() => stockDraft.fromBarcode && stockDraft.toBarcode && stockDraft.fromBarcode <= stockDraft.toBarcode ? pendingProduction.filter((label) => label.barcodeValue >= stockDraft.fromBarcode && label.barcodeValue <= stockDraft.toBarcode).length : 0, [pendingProduction, stockDraft.fromBarcode, stockDraft.toBarcode]);
  const dispatchCustomers = customers.filter((customer) => `${customer.legacyId ?? ""} ${customer.name} ${customer.city}`.toLowerCase().includes(dispatchDraft.customerSearch.toLowerCase()));
  const packingListLines = useMemo(() => {
    if (!dispatchResult) return [];
    return Array.from(dispatchResult.labels.reduce((lines, label) => {
      const key = `${label.itemCode}-${label.itemName}-${label.piecesPerBox}-${label.grossWeightKg}`;
      const current = lines.get(key) ?? { itemName: label.itemName, piecesPerBox: label.piecesPerBox, boxes: 0, grossWeightKg: label.grossWeightKg };
      current.boxes += 1; lines.set(key, current); return lines;
    }, new Map<string, { itemName: string; piecesPerBox: number; boxes: number; grossWeightKg: number }>()).values()).map((line) => ({ ...line, totalPieces: line.piecesPerBox * line.boxes, totalGrossWeightKg: line.grossWeightKg * line.boxes }));
  }, [dispatchResult]);

  function notify(text: string) {
    setMessage(text);
    window.setTimeout(() => setMessage(""), 2800);
  }

  useEffect(() => {
    if (!apiToken || (active !== "Master" && active !== "Label" && active !== "Reports")) return;
    fetch(`${apiUrl}/api/departments?page=1&pageSize=100`, {
      headers: { Authorization: `Bearer ${apiToken}` },
    })
      .then(async (response) =>
        response.ok
          ? response.json()
          : Promise.reject(new Error("Unable to load departments from the database.")),
      )
      .then((page: { items: Department[] }) => setDepartments(page.items))
      .catch(() => notify("The department database could not be reached."));
  }, [active, apiToken]);

  useEffect(() => {
    if (!apiToken || (active !== "Customer" && active !== "Dispatch" && active !== "Reports")) return;
    fetch(`${apiUrl}/api/customers?page=1&pageSize=100`, { headers: { Authorization: `Bearer ${apiToken}` } })
      .then(async (response) => response.ok ? response.json() : Promise.reject(new Error("Unable to load customers from the database.")))
      .then((page: { items: ApiCustomer[] }) => setCustomers(page.items.map(fromApiCustomer)))
      .catch(() => notify("The customer database could not be reached."));
  }, [active, apiToken]);

  useEffect(() => {
    if (!apiToken || (active !== "Label" && active !== "Reports" && (active !== "Master" || masterTab !== "items"))) return;
    fetch(`${apiUrl}/api/items?page=1&pageSize=100`, {
      headers: { Authorization: `Bearer ${apiToken}` },
    })
      .then(async (response) =>
        response.ok
          ? response.json()
          : Promise.reject(
              new Error("Unable to load items from the database."),
            ),
      )
      .then((page: { items: ApiItem[] }) =>
        setItems(page.items.map(fromApiItem)),
      )
      .catch(() =>
        notify(
          "The database could not be reached. Showing local preview data.",
        ),
      );
  }, [active, apiToken, masterTab]);

  useEffect(() => {
    if (!apiToken || active !== "Production") return;
    const query = new URLSearchParams({ page: "1", pageSize: "500" });
    if (productionDepartmentId) query.set("departmentId", productionDepartmentId);
    if (productionSearch.trim()) query.set("search", productionSearch.trim());
    if (isProductionMonthFilterOn && /^\d{4}-\d{2}$/.test(productionMonth)) { const [year, month] = productionMonth.split("-"); query.set("manufactureYear", year); query.set("manufactureMonth", String(Number(month))); }
    fetch(`${apiUrl}/api/production/pending?${query}`, { headers: { Authorization: `Bearer ${apiToken}` } })
      .then(async (response) => response.ok ? response.json() : Promise.reject(new Error("Unable to load production labels.")))
      .then((page: { items: PendingProductionLabel[]; total: number }) => { setPendingProduction(page.items); setProductionTotal(page.total); })
      .catch(() => notify("The production database could not be reached."));
  }, [active, apiToken, productionDepartmentId, productionSearch, isProductionMonthFilterOn, productionMonth]);

  useEffect(() => {
    if (!apiToken || (active !== "Label" && active !== "More")) return;
    if (active === "Label") {
      fetch(`${apiUrl}/api/labels/logos`, { headers: { Authorization: `Bearer ${apiToken}` } })
        .then(async (response) => response.ok ? response.json() : Promise.reject(new Error("Unable to load label logos.")))
        .then((rows: LabelLogo[]) => setLabelLogos(rows))
        .catch(() => notify("Label logos could not be loaded."));
    }
    loadTemplates();
  }, [active, apiToken]);

  useEffect(() => {
    if (!apiToken || (active !== "Reports" && active !== "Production")) return;
    const headers = { Authorization: `Bearer ${apiToken}` };
    const fromProduction = () => fetch(`${apiUrl}/api/production/batches`, { headers })
      .then(async (response) => response.ok ? response.json() : Promise.reject(new Error(`Production batches responded ${response.status}.`)))
      .then((rows: string[]) => setBatchOptions((current) => Array.from(new Set([...current, ...(rows ?? []).filter(Boolean)]))));
    fetch(`${apiUrl}/api/reports/batches`, { headers })
      .then(async (response) => response.ok ? response.json() : Promise.reject(new Error(`Report batches responded ${response.status}.`)))
      .then((rows: string[]) => {
        const rows_ = (rows ?? []).filter(Boolean);
        setBatchOptions((current) => Array.from(new Set([...current, ...rows_])));
        if (rows_.length === 0) return fromProduction();
      })
      .catch((error) => { console.warn("Batch list fallback:", error); return fromProduction(); });
  }, [active, apiToken, reportTab]);

  useEffect(() => {
    if (!apiToken || active !== "Dispatch" || dispatchTab !== "history") return;
    const query = new URLSearchParams({ page: "1", pageSize: "100" });
    if (oldDispatchSearch.salesOrderNumber.trim()) query.set("salesOrderNumber", oldDispatchSearch.salesOrderNumber.trim());
    if (oldDispatchSearch.invoiceNumber.trim()) query.set("invoiceNumber", oldDispatchSearch.invoiceNumber.trim());
    if (oldDispatchSearch.customer.trim()) query.set("customer", oldDispatchSearch.customer.trim());
    if (oldDispatchSearch.dispatchDate) query.set("dispatchDate", oldDispatchSearch.dispatchDate);
    if (oldDispatchSearch.barcode.trim()) query.set("barcode", oldDispatchSearch.barcode.trim());
    fetch(`${apiUrl}/api/dispatch?${query}`, { headers: { Authorization: `Bearer ${apiToken}` } })
      .then(async (response) => response.ok ? response.json() : Promise.reject(new Error("Unable to load dispatch history.")))
      .then((page: { items: DispatchHistoryRow[]; total: number }) => { setOldDispatches(page.items); setOldDispatchTotal(page.total); })
      .catch(() => notify("The dispatch history database could not be reached."));
  }, [active, apiToken, dispatchTab, oldDispatchSearch]);

  function loadTemplates() {
    if (!apiToken) return;
    fetch(`${apiUrl}/api/label-templates`, { headers: { Authorization: `Bearer ${apiToken}` } })
      .then((res) => (res.ok ? res.json() : []))
      .then((tpls: LabelTemplate[]) => {
        setLabelTemplates(tpls);
        setLabelDraft((prev) => {
          if (prev.templateId && tpls.some((t) => String(t.id) === prev.templateId)) return prev;
          const defaultTpl = tpls.find((t) => t.isDefault) || tpls[0];
          return defaultTpl ? { ...prev, templateId: String(defaultTpl.id) } : prev;
        });
      })
      .catch((err) => console.error("Failed to load label templates:", err));
  }

  function loadPrintersAndConfig() {
    if (!apiToken) return;
    setLoadingPrinters(true);
    const headers = { Authorization: `Bearer ${apiToken}` };
    Promise.all([
      fetch(`${apiUrl}/api/printers/available`, { headers }).then((res) => res.ok ? res.json() : []),
      fetch(`${apiUrl}/api/printers/configuration`, { headers }).then((res) => res.ok ? res.json() : { regularDocumentPrinter: null, barcodePrinter: null }),
      fetch(`${apiUrl}/api/printers/jobs?limit=20`, { headers }).then((res) => res.ok ? res.json() : []),
      fetch(`${apiUrl}/api/label-templates`, { headers }).then((res) => res.ok ? res.json() : [])
    ])
      .then(([printers, config, jobs, tpls]: [AvailablePrinter[], PrinterConfigurationsSummary, PrintJobRow[], LabelTemplate[]]) => {
        setAvailablePrinters(printers);
        setPrinterConfig(config);
        setLabelTemplates(tpls);
        if (tpls && tpls.length > 0) {
          setLabelDraft((prev) => {
            if (prev.templateId && tpls.some((t) => String(t.id) === prev.templateId)) return prev;
            const target = (config.barcodePrinter?.activeTemplateId && tpls.find((t) => t.id === config.barcodePrinter!.activeTemplateId))
              || tpls.find((t) => t.isDefault)
              || tpls[0];
            return target ? { ...prev, templateId: String(target.id) } : prev;
          });
        }
        if (config.regularDocumentPrinter?.printerName) {
          setSelectedRegularPrinter(config.regularDocumentPrinter.printerName);
        }
        if (config.barcodePrinter?.printerName) {
          setSelectedBarcodePrinter(config.barcodePrinter.printerName);
          if (config.barcodePrinter.activeTemplateId) {
            setSelectedBarcodeTemplateId(config.barcodePrinter.activeTemplateId);
          }
          if (config.barcodePrinter.mode === 2) {
            setBarcodeMode("tsc");
          } else {
            setBarcodeMode("laser");
          }
          if (config.barcodePrinter.paperSize) setLaserPaperSize(config.barcodePrinter.paperSize);
          if (config.barcodePrinter.paperWidthMm) setLaserPaperWidthMm(config.barcodePrinter.paperWidthMm);
          if (config.barcodePrinter.paperHeightMm) setLaserPaperHeightMm(config.barcodePrinter.paperHeightMm);
          if (config.barcodePrinter.marginLeftMm != null) setLaserMarginLeftMm(config.barcodePrinter.marginLeftMm);
          if (config.barcodePrinter.marginRightMm != null) setLaserMarginRightMm(config.barcodePrinter.marginRightMm);
          if (config.barcodePrinter.marginTopMm != null) setLaserMarginTopMm(config.barcodePrinter.marginTopMm);
          if (config.barcodePrinter.marginBottomMm != null) setLaserMarginBottomMm(config.barcodePrinter.marginBottomMm);
          if (config.barcodePrinter.horizontalGapMm != null) setLaserGapXMm(config.barcodePrinter.horizontalGapMm);
          if (config.barcodePrinter.verticalGapMm != null) setLaserGapYMm(config.barcodePrinter.verticalGapMm);
        }
        setRecentPrintJobs(jobs);
      })
      .catch((err) => {
        console.error("Failed to load printer data:", err);
        notify("Unable to reach server printer subsystem.");
      })
      .finally(() => setLoadingPrinters(false));
  }

  function loadRecentPrintJobs() {
    if (!apiToken) return;
    fetch(`${apiUrl}/api/printers/jobs?limit=20`, { headers: { Authorization: `Bearer ${apiToken}` } })
      .then((res) => res.ok ? res.json() : [])
      .then((jobs: PrintJobRow[]) => setRecentPrintJobs(jobs))
      .catch(() => {});
  }

  function loadUserAccounts() {
    if (!apiToken || currentUserRole !== "Admin") return;
    setLoadingUsers(true);
    fetch(`${apiUrl}/api/users`, { headers: { Authorization: `Bearer ${apiToken}` } })
      .then(async (res) => res.ok ? res.json() : [])
      .then((users: UserAccount[]) => setUserAccounts(users))
      .catch(() => notify("Could not load user accounts."))
      .finally(() => setLoadingUsers(false));
  }

  useEffect(() => {
    if (active === "User" && apiToken && currentUserRole === "Admin") {
      loadUserAccounts();
    }
  }, [active, apiToken, currentUserRole]);

  function savePrinterConfig(category: "regular" | "barcode") {
    if (!apiToken) return;
    const targetPrinter = category === "regular" ? selectedRegularPrinter : selectedBarcodePrinter;
    if (!targetPrinter) {
      notify("Please select a printer first.");
      return;
    }
    setSavingPrinter(true);
    const bodyPayload = category === "regular"
      ? { printerName: targetPrinter }
      : {
          printerName: targetPrinter,
          mode: barcodeMode === "tsc" ? 2 : 1,
          model: barcodeMode === "tsc" ? "TSC TTP-247" : "Laser",
          dpi: barcodeMode === "tsc" ? 203 : 600,
          activeTemplateId: selectedBarcodeTemplateId ?? printerConfig.barcodePrinter?.activeTemplateId ?? null,
          paperSize: laserPaperSize,
          paperWidthMm: laserPaperWidthMm,
          paperHeightMm: laserPaperHeightMm,
          marginLeftMm: laserMarginLeftMm,
          marginRightMm: laserMarginRightMm,
          marginTopMm: laserMarginTopMm,
          marginBottomMm: laserMarginBottomMm,
          horizontalGapMm: laserGapXMm,
          verticalGapMm: laserGapYMm
        };

    fetch(`${apiUrl}/api/printers/configuration/${category}`, {
      method: "PUT",
      headers: { Authorization: `Bearer ${apiToken}`, "Content-Type": "application/json" },
      body: JSON.stringify(bodyPayload)
    })
      .then(async (res) => {
        const body = await res.json();
        if (!res.ok) throw new Error(body.message || "Failed to update printer configuration.");
        return body;
      })
      .then(() => {
        notify(`${category === "regular" ? "Regular document" : `Barcode (${barcodeMode.toUpperCase()})`} printer updated successfully.`);
        loadPrintersAndConfig();
      })
      .catch((err) => notify(err.message || "Failed to save printer."))
      .finally(() => setSavingPrinter(false));
  }

  function runTestPrint(category: "RegularDocument" | "Barcode") {
    if (!apiToken) return;
    setTestingPrinter(true);
    fetch(`${apiUrl}/api/printers/test-print`, {
      method: "POST",
      headers: { Authorization: `Bearer ${apiToken}`, "Content-Type": "application/json" },
      body: JSON.stringify({ category })
    })
      .then(async (res) => {
        const body = await res.json();
        if (!res.ok) throw new Error(body.message || "Test print failed.");
        return body;
      })
      .then((job: PrintJobRow) => {
        notify(`Test print #${job.id} sent successfully to ${job.printerName}.`);
        loadPrintersAndConfig();
      })
      .catch((err) => notify(err.message || "Test print failed."))
      .finally(() => setTestingPrinter(false));
  }

  useEffect(() => {
    if (!apiToken || active !== "More") return;
    loadPrintersAndConfig();
  }, [active, apiToken]);

  async function login(event: FormEvent) {
    event.preventDefault();
    setLoginError("");
    if (!userName.trim() || !password) {
      setLoginError("Enter your username and password.");
      return;
    }
    try {
      const response = await fetch(`${apiUrl}/api/auth/login`, {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ userName, password }),
      });
      if (!response.ok) {
        const body = await response.json().catch(() => ({}));
        setLoginError(body.message ?? "Invalid username or password.");
        return;
      }
      const body = await response.json();
      setApiToken(body.token);
      const role = body.role ?? "Admin";
      setCurrentUserRole(role);
      setCurrentUserDeptId(body.departmentId ?? null);
      setCurrentUserDeptName(body.departmentName ?? null);
      setCurrentUsername(body.username ?? userName);
      if (role === "Production") {
        setActive("Production");
        if (body.departmentId) {
          setProductionDepartmentId(String(body.departmentId));
          setReportDepartmentId(String(body.departmentId));
        }
      } else if (role === "QC") {
        setActive("Production");
      } else {
        setActive("Master");
      }
    } catch {
      /* Local preview remains usable while the API is offline. */
    }
    setLoggedIn(true);
  }

  function resetDepartment() {
    setDepartmentDraft({ id: 0, name: "" });
  }

  function resetCustomer() { setCustomerDraft({ id: 0, name: "", address1: "", address2: "", city: "", pincode: "", state: "", country: "" }); }
  async function saveCustomer(event: FormEvent) {
    event.preventDefault();
    if (!customerDraft.name.trim()) { notify("Customer name is required."); return; }
    const payload = { legacyId: customerDraft.legacyId || null, name: customerDraft.name, address1: customerDraft.address1, address2: customerDraft.address2, city: customerDraft.city, pincode: customerDraft.pincode, state: customerDraft.state, country: customerDraft.country };
    if (apiToken) {
      const response = await fetch(`${apiUrl}/api/customers${customerDraft.id ? `/${customerDraft.id}` : ""}`, { method: customerDraft.id ? "PUT" : "POST", headers: { Authorization: `Bearer ${apiToken}`, "Content-Type": "application/json" }, body: JSON.stringify(payload) });
      const body = await response.json().catch(() => ({}));
      if (!response.ok) { notify(body.message ?? "The customer could not be saved."); return; }
      const saved = fromApiCustomer(body as ApiCustomer);
      setCustomers(customerDraft.id ? customers.map((customer) => customer.id === saved.id ? saved : customer) : [...customers, saved]);
    } else if (customerDraft.id) setCustomers(customers.map((customer) => customer.id === customerDraft.id ? { ...customerDraft, name: customerDraft.name.trim() } : customer));
    else setCustomers([...customers, { ...customerDraft, id: Math.max(0, ...customers.map((customer) => customer.id)) + 1, name: customerDraft.name.trim() }]);
    resetCustomer();
    notify("Customer saved.");
  }
  async function deleteCustomer(id: number) {
    if (!window.confirm("Delete this customer?")) return;
    if (apiToken) { const response = await fetch(`${apiUrl}/api/customers/${id}`, { method: "DELETE", headers: { Authorization: `Bearer ${apiToken}` } }); if (!response.ok) { const body = await response.json().catch(() => ({})); notify(body.message ?? "The customer could not be deleted."); return; } }
    setCustomers(customers.filter((customer) => customer.id !== id));
    notify("Customer deleted.");
  }
  async function exportCustomers(format: "csv" | "xls" | "xlsx") {
    if (apiToken) {
      const response = await fetch(`${apiUrl}/api/customers/export?format=${format}`, { headers: { Authorization: `Bearer ${apiToken}` } });
      if (!response.ok) {
        const body = await response.json().catch(() => ({}));
        notify(body.message ?? "Customer export failed.");
        return;
      }
      const blob = await response.blob();
      downloadBlob(blob, `boxtrack-customers.${format}`);
      notify(`Customer ${format.toUpperCase()} export downloaded.`);
      return;
    }

    const csv = ["C_ID,C_Name,C_Add1,C_Add2,C_City,C_Pin,C_State,C_Country", ...customers.map((customer) => `${customer.legacyId ?? customer.id},"${customer.name}","${customer.address1}","${customer.address2}","${customer.city}","${customer.pincode}","${customer.state}","${customer.country}"`)].join("\n");
    downloadBlob(new Blob([csv], { type: "text/csv" }), "boxtrack-customers.csv");
    notify(format === "csv" ? "Customer CSV export downloaded." : "Start the API to export spreadsheet files. CSV downloaded instead.");
  }

  async function importCustomers(event: ChangeEvent<HTMLInputElement>) {
    const file = event.target.files?.[0];
    if (!file) return;
    if (!apiToken) {
      notify("Start the API and sign in again to import customer CSV, XLS, or XLSX files.");
      event.target.value = "";
      return;
    }

    const form = new FormData();
    form.append("file", file);
    try {
      const response = await fetch(`${apiUrl}/api/customers/import`, { method: "POST", headers: { Authorization: `Bearer ${apiToken}` }, body: form });
      const body = await response.json();
      if (!response.ok) {
        notify(body.message ?? "Customer import failed.");
        return;
      }
      setImportIssues(body.issues ?? []);
      setDuplicateComparisons([]);
      setCustomerDuplicateComparisons(body.duplicates ?? []);
      notify(`${body.imported} customer${body.imported === 1 ? "" : "s"} imported.`);
      const refreshed = await fetch(`${apiUrl}/api/customers?page=1&pageSize=100`, { headers: { Authorization: `Bearer ${apiToken}` } }).then((result) => result.json());
      setCustomers(refreshed.items.map((customer: ApiCustomer) => fromApiCustomer(customer)));
    } catch {
      notify("Customer import failed because the database API is unavailable.");
    } finally {
      event.target.value = "";
    }
  }

  function downloadBlob(blob: Blob, fileName: string) {
    const link = document.createElement("a");
    link.href = URL.createObjectURL(blob);
    link.download = fileName;
    link.click();
    URL.revokeObjectURL(link.href);
  }
  async function saveDepartment(event: FormEvent) {
    event.preventDefault();
    const name = departmentDraft.name.trim();
    if (!name) {
      notify("Department name is required.");
      return;
    }
    if (
      departments.some(
        (department) =>
          department.name.toLowerCase() === name.toLowerCase() &&
          department.id !== departmentDraft.id,
      )
    ) {
      notify("Department names must be unique.");
      return;
    }
    if (apiToken) {
      const response = await fetch(`${apiUrl}/api/departments${departmentDraft.id ? `/${departmentDraft.id}` : ""}`, { method: departmentDraft.id ? "PUT" : "POST", headers: { Authorization: `Bearer ${apiToken}`, "Content-Type": "application/json" }, body: JSON.stringify({ name }) });
      const body = await response.json().catch(() => ({}));
      if (!response.ok) { notify(body.message ?? "The department could not be saved."); return; }
      setDepartments(departmentDraft.id ? departments.map((department) => department.id === body.id ? body : department) : [...departments, body]);
    } else if (departmentDraft.id) setDepartments(departments.map((department) => department.id === departmentDraft.id ? { ...department, name } : department));
    else setDepartments([...departments, { id: Math.max(0, ...departments.map((department) => department.id)) + 1, name }]);
    resetDepartment();
    notify("Department saved.");
  }

  async function deleteDepartment(id: number) {
    if (items.some((item) => item.departmentId === id)) {
      notify("This department cannot be deleted because items exist in it.");
      return;
    }
    if (window.confirm("Delete this department?")) {
      if (apiToken) {
        const response = await fetch(`${apiUrl}/api/departments/${id}`, { method: "DELETE", headers: { Authorization: `Bearer ${apiToken}` } });
        const body = await response.json().catch(() => ({}));
        if (!response.ok) { notify(body.message ?? "The department could not be deleted."); return; }
      }
      setDepartments(departments.filter((department) => department.id !== id));
      notify("Department deleted.");
    }
  }

  function resetItem() {
    setItemDraft({
      id: 0,
      name: "",
      description: "",
      packing: "",
      departmentId: String(departments[0]?.id ?? ""),
      weight: "",
    });
  }
  async function saveItem(event: FormEvent) {
    event.preventDefault();
    const name = itemDraft.name.trim();
    const packing = Number(itemDraft.packing);
    const weight = Number(itemDraft.weight);
    const departmentId = Number(itemDraft.departmentId);
    if (!name || !itemDraft.packing || !itemDraft.weight || !departmentId) {
      notify("Complete all required item fields.");
      return;
    }
    if (!Number.isInteger(packing) || packing <= 0) {
      notify("Packing must be a whole number greater than zero.");
      return;
    }
    if (!Number.isFinite(weight) || weight <= 0) {
      notify("Gross weight must be greater than zero.");
      return;
    }
    const value = {
      name,
      description: itemDraft.description.trim(),
      packagingPerBox: packing,
      grossWeightKg: weight,
      departmentId,
    };
    if (apiToken) {
      const response = await fetch(
        `${apiUrl}/api/items${itemDraft.id ? `/${itemDraft.id}` : ""}`,
        {
          method: itemDraft.id ? "PUT" : "POST",
          headers: {
            Authorization: `Bearer ${apiToken}`,
            "Content-Type": "application/json",
          },
          body: JSON.stringify(value),
        },
      );
      const body = await response.json().catch(() => ({}));
      if (!response.ok) {
        notify(body.message ?? "The item could not be saved.");
        return;
      }
      const saved = fromApiItem(body as ApiItem);
      setItems(
        itemDraft.id
          ? items.map((item) => (item.id === saved.id ? saved : item))
          : [...items, saved],
      );
    } else {
      const localValue = {
        name,
        description: itemDraft.description.trim(),
        packing,
        departmentId,
        weight,
      };
      if (itemDraft.id)
        setItems(
          items.map((item) =>
            item.id === itemDraft.id ? { ...item, ...localValue } : item,
          ),
        );
      else {
        if (nextCode > 999) {
          notify("No item codes remain. The maximum is 999.");
          return;
        }
        setItems([
          ...items,
          {
            id: Math.max(0, ...items.map((item) => item.id)) + 1,
            code: nextCode,
            ...localValue,
          },
        ]);
      }
    }
    resetItem();
    notify(
      nextCode > 950
        ? "Item saved. Warning: item codes are nearly exhausted."
        : "Item saved.",
    );
  }

  async function deleteItem(id: number) {
    if (!window.confirm("Delete this item?")) return;
    if (apiToken) {
      const response = await fetch(`${apiUrl}/api/items/${id}`, {
        method: "DELETE",
        headers: { Authorization: `Bearer ${apiToken}` },
      });
      if (!response.ok) {
        const body = await response.json().catch(() => ({}));
        notify(body.message ?? "The item could not be deleted.");
        return;
      }
    }
    setItems(items.filter((item) => item.id !== id));
    notify("Item deleted.");
  }

  function exportItems() {
    const csv = [
      "Item ID,Item Name,Description,Department,Packing,Gross Weight,Department ID",
      ...items.map(
        (item) =>
          `${item.code},"${item.name}","${item.description}","${departmentName(item.departmentId)}",${item.packing},${item.weight},${item.departmentId}`,
      ),
    ].join("\n");
    const link = document.createElement("a");
    link.href = URL.createObjectURL(new Blob([csv], { type: "text/csv" }));
    link.download = "boxtrack-items.csv";
    link.click();
    URL.revokeObjectURL(link.href);
    notify("Item export downloaded.");
  }

  async function importItems(event: ChangeEvent<HTMLInputElement>) {
    const file = event.target.files?.[0];
    if (!file) return;
    if (apiToken) {
      const form = new FormData();
      form.append("file", file);
      try {
        const response = await fetch(`${apiUrl}/api/items/import`, {
          method: "POST",
          headers: { Authorization: `Bearer ${apiToken}` },
          body: form,
        });
        const body = await response.json();
        if (!response.ok) {
          notify(body.message ?? "Import failed.");
          return;
        }
        setImportIssues(body.issues ?? []);
        setDuplicateComparisons(body.duplicates ?? []);
        notify(
          `${body.imported} item${body.imported === 1 ? "" : "s"} imported.`,
        );
        const refreshed = await fetch(
          `${apiUrl}/api/items?page=1&pageSize=100`,
          { headers: { Authorization: `Bearer ${apiToken}` } },
        ).then((result) => result.json());
        setItems(refreshed.items.map((item: ApiItem) => fromApiItem(item)));
      } catch {
        notify("Import failed because the database API is unavailable.");
      }
    } else if (file.name.toLowerCase().endsWith(".csv")) {
      const lines = (await file.text()).split(/\r?\n/).slice(1).filter(Boolean);
      const issues: ImportIssue[] = [];
      const comparisons: ItemDuplicateComparison[] = [];
      const additions: Item[] = [];
      lines.forEach((line, index) => {
        const columns = line
          .split(",")
          .map((value) => value.replace(/^"|"$/g, "").trim());
        const code = Number(columns[0]);
        const existing = items.find((item) => item.code === code);
        if (existing) {
          comparisons.push({
            row: index + 2,
            existing: { id: existing.id, code: existing.code, name: existing.name, description: existing.description, packagingPerBox: existing.packing, grossWeightKg: existing.weight, departmentId: existing.departmentId, departmentName: departmentName(existing.departmentId) },
            uploaded: { code, name: columns[1], description: columns[2], packagingPerBox: Number(columns[4]), grossWeightKg: Number(columns[5]), department: columns[3], departmentId: Number(columns[6]) },
          });
          issues.push({ row: index + 2, code: columns[0], message: "Item ID already exists in the database. Review the comparison and update it explicitly if needed." });
        } else if (
          !Number.isInteger(code) ||
          additions.some((item) => item.code === code)
        )
          issues.push({
            row: index + 2,
            code: columns[0],
            message: "Duplicate or invalid Item ID.",
          });
        else
          additions.push({
            id:
              Math.max(
                0,
                ...items.map((item) => item.id),
                ...additions.map((item) => item.id),
              ) + 1,
            code,
            name: columns[1] ?? "",
            description: columns[2] ?? "",
            departmentId: Number(columns[6]) || departments[0].id,
            packing: Number(columns[4]),
            weight: Number(columns[5]),
          });
      });
      setItems([...items, ...additions]);
      setImportIssues(issues);
      setDuplicateComparisons(comparisons);
      notify(
        `${additions.length} item${additions.length === 1 ? "" : "s"} imported into the preview.`,
      );
    } else notify("Database API is offline. Start PostgreSQL and the API, then sign in again to import .xls or .xlsx files.");
    event.target.value = "";
  }

  function closeImportReview() {
    setImportIssues([]);
    setDuplicateComparisons([]);
    setCustomerDuplicateComparisons([]);
  }

  function reviewExisting(item: ItemDuplicateComparison["existing"]) {
    setItemDraft({ id: item.id, name: item.name, description: item.description ?? "", packing: String(item.packagingPerBox), departmentId: String(item.departmentId), weight: String(item.grossWeightKg) });
    closeImportReview();
    setMasterTab("items");
    notify("Existing database item loaded. Edit the fields and select Update.");
  }

  function reviewUploaded(comparison: ItemDuplicateComparison) {
    const uploaded = comparison.uploaded;
    setItemDraft({ id: comparison.existing.id, name: uploaded.name ?? "", description: uploaded.description ?? "", packing: uploaded.packagingPerBox ? String(uploaded.packagingPerBox) : "", departmentId: String(uploaded.departmentId ?? comparison.existing.departmentId), weight: uploaded.grossWeightKg ? String(uploaded.grossWeightKg) : "" });
    closeImportReview();
    setMasterTab("items");
    notify("Uploaded values loaded for review. The Item ID remains unchanged.");
  }

  function reviewExistingCustomer(customer: ApiCustomer) {
    setCustomerDraft(fromApiCustomer(customer));
    closeImportReview();
    setActive("Customer");
    notify("Existing database customer loaded. Edit the fields and select Update.");
  }

  function reviewUploadedCustomer(comparison: CustomerDuplicateComparison) {
    setCustomerDraft({ id: comparison.existing.id, legacyId: comparison.uploaded.legacyId, name: comparison.uploaded.name ?? "", address1: comparison.uploaded.address1 ?? "", address2: comparison.uploaded.address2 ?? "", city: comparison.uploaded.city ?? "", pincode: comparison.uploaded.pincode ?? "", state: comparison.uploaded.state ?? "", country: comparison.uploaded.country ?? "" });
    closeImportReview();
    setActive("Customer");
    notify("Uploaded customer values loaded for review. The database Customer ID remains unchanged.");
  }

  async function uploadLabelLogo(event: FormEvent) {
    event.preventDefault();
    if (!apiToken) { notify("Sign in with the API online before uploading logos."); return; }
    if (!logoUpload.name.trim() || !logoUpload.file) { notify("Enter a logo name and choose a file."); return; }
    const form = new FormData();
    form.append("name", logoUpload.name.trim());
    form.append("file", logoUpload.file);
    const response = await fetch(`${apiUrl}/api/labels/logos`, { method: "POST", headers: { Authorization: `Bearer ${apiToken}` }, body: form });
    const body = await response.json().catch(() => ({}));
    if (!response.ok) { notify(body.message ?? "Logo upload failed."); return; }
    const uploadedLogo = body as LabelLogo;
    setLabelLogos([...labelLogos, uploadedLogo]);
    setLogoUpload({ name: "", file: null });
    setIsLogoDialogOpen(false);
    notify("Logo uploaded.");
  }

  async function generateLabels(event: FormEvent) {
    event.preventDefault();
    if (!apiToken) { notify("Sign in with the API online before generating labels."); return; }
    const quantity = Number(labelDraft.quantity);
    if (!labelDraft.itemId || !labelDraft.manufactureDate || !Number.isInteger(quantity) || quantity <= 0) { notify("Select item, date, and a valid quantity."); return; }
    if (!labelDraft.templateId) { notify("Please select a Label Template to generate and print."); return; }
    const response = await fetch(`${apiUrl}/api/labels/generate`, {
      method: "POST",
      headers: { Authorization: `Bearer ${apiToken}`, "Content-Type": "application/json" },
      body: JSON.stringify({
        itemId: Number(labelDraft.itemId),
        manufactureDate: labelDraft.manufactureDate,
        quantity,
        templateId: Number(labelDraft.templateId)
      }),
    });
    const body = await response.json().catch(() => ({}));
    if (!response.ok) { notify(body.message ?? "Barcode generation failed."); return; }
    setLabelRange({ from: body.fromBarcode, to: body.toBarcode });
    if (body.printerName) {
      notify(`${body.generated} barcode label${body.generated === 1 ? "" : "s"} generated & sent to Barcode Printer '${body.printerName}'.`);
    } else {
      notify(`${body.generated} barcode label${body.generated === 1 ? "" : "s"} generated. (No Barcode Printer configured in Admin > More > Printing).`);
    }
    loadRecentPrintJobs();
  }

  async function addLabelsToStock(event: FormEvent) {
    event.preventDefault();
    if (!apiToken) { notify("Sign in with the API online before adding stock."); return; }
    if (!stockDraft.batchNumber.trim() || !stockDraft.fromBarcode || !stockDraft.toBarcode) { notify("Enter a batch number and select the barcode range."); return; }
    const response = await fetch(`${apiUrl}/api/production/add-to-stock`, { method: "POST", headers: { Authorization: `Bearer ${apiToken}`, "Content-Type": "application/json" }, body: JSON.stringify(stockDraft) });
    const body = await response.json().catch(() => ({}));
    if (!response.ok) { notify(body.message ?? "Labels could not be added to stock."); return; }
    notify(`${body.quantity} label${body.quantity === 1 ? "" : "s"} added to stock for batch ${body.batchNumber}.`);
    if (body.batchNumber && !batchOptions.includes(body.batchNumber)) setBatchOptions([body.batchNumber, ...batchOptions]);
    setStockDraft({ batchNumber: "", fromBarcode: "", toBarcode: "" });
    const query = new URLSearchParams({ page: "1", pageSize: "500" });
    if (productionDepartmentId) query.set("departmentId", productionDepartmentId);
    if (productionSearch.trim()) query.set("search", productionSearch.trim());
    if (isProductionMonthFilterOn && /^\d{4}-\d{2}$/.test(productionMonth)) { const [year, month] = productionMonth.split("-"); query.set("manufactureYear", year); query.set("manufactureMonth", String(Number(month))); }
    const refreshed = await fetch(`${apiUrl}/api/production/pending?${query}`, { headers: { Authorization: `Bearer ${apiToken}` } }).then((result) => result.json());
    setPendingProduction(refreshed.items ?? []); setProductionTotal(refreshed.total ?? 0);
  }

  async function generateSalesOrder(event: FormEvent) {
    event.preventDefault();
    if (!apiToken) { notify("Sign in with the API online before dispatching labels."); return; }
    if (!dispatchDraft.customerId || !dispatchDraft.salesOrderNumber.trim() || !dispatchDraft.invoiceNumber.trim() || !dispatchDraft.dispatchDate || !dispatchDraft.file) { notify("Select a customer, enter sales order and invoice numbers, date, and scanner file."); return; }
    // Do not let a previous dispatch remain available while a new sales order is being created.
    setDispatchResult(null);
    setIsPackingPreviewOpen(false);
    const form = new FormData();
    form.append("customerId", dispatchDraft.customerId); form.append("salesOrderNumber", dispatchDraft.salesOrderNumber.trim()); form.append("invoiceNumber", dispatchDraft.invoiceNumber.trim()); form.append("dispatchDate", dispatchDraft.dispatchDate); form.append("file", dispatchDraft.file);
    const response = await fetch(`${apiUrl}/api/dispatch`, { method: "POST", headers: { Authorization: `Bearer ${apiToken}` }, body: form });
    const body = await response.json().catch(() => ({}));
    if (!response.ok) { notify(body.message ?? "Dispatch could not be completed."); return; }
    setDispatchResult(body as DispatchResult);
    setDispatchDraft({ ...dispatchDraft, file: null });
    setDispatchPreviewKind("salesOrder");
    setIsPackingPreviewOpen(true);
    notify(`Sales order generated. ${body.dispatched} barcode label${body.dispatched === 1 ? "" : "s"} marked as dispatched.`);
  }

  function fallbackBrowserPrint(htmlContent: string) {
    const printWindow = window.open("", "_blank", "noopener,noreferrer,width=900,height=1000");
    if (!printWindow) {
      notify("Allow pop-ups to print the document.");
      return;
    }
    printWindow.document.write(htmlContent);
    printWindow.document.close();
  }

  async function printDocumentToServer(docPayload: { title: string; subtitle?: string; address?: string[]; headers: string[]; rows: string[][] }, fallbackHtmlBuilder: () => string) {
    if (apiToken && printerConfig.regularDocumentPrinter?.printerName) {
      try {
        const response = await fetch(`${apiUrl}/api/printers/print-document`, {
          method: "POST",
          headers: { Authorization: `Bearer ${apiToken}`, "Content-Type": "application/json" },
          body: JSON.stringify(docPayload),
        });
        const body = await response.json().catch(() => ({}));
        if (response.ok) {
          notify(`Sent to Regular Document Printer '${body.printerName}' (Job #${body.id}).`);
          loadRecentPrintJobs();
          return;
        } else {
          notify(body.message ?? "Server printing failed. Opening browser print...");
        }
      } catch {
        notify("Server printing unavailable. Opening browser print...");
      }
    }
    fallbackBrowserPrint(fallbackHtmlBuilder());
  }

  async function printPackingList() {
    if (!dispatchResult) return;
    const escapeHtml = (value: string) => value.replace(/[&<>"']/g, (character) => ({ "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;", "'": "&#39;" }[character] ?? character));
    const addressLines = packingAddressLines(dispatchResult.customer);
    const headers = ["Invoice No.", "SO No.", "Dispatch Date", "Batch No.", "Item", "Box No."];
    const rows = dispatchResult.labels.map((label) => [
      dispatchResult.invoiceNumber,
      dispatchResult.salesOrderNumber,
      dispatchResult.dispatchDate,
      label.batchNumber,
      label.itemName,
      label.barcodeValue,
    ]);

    const fallbackHtml = () => {
      const address = addressLines.map((line) => `<span>${escapeHtml(line)}</span>`).join("");
      const htmlRows = rows.map((r) => `<tr>${r.map((c) => `<td>${escapeHtml(c)}</td>`).join("")}</tr>`).join("");
      return `<!doctype html><html><head><title>Packing List ${escapeHtml(dispatchResult.salesOrderNumber)}</title><style>@page{size:A4 portrait;margin:0}*{box-sizing:border-box}body{margin:0;color:#111;font-family:Arial,sans-serif}.sheet{width:210mm;min-height:297mm;padding:14mm 12mm}.header{display:flex;justify-content:space-between;gap:24px;min-height:43mm;font-size:11pt;line-height:1.55}.address{display:grid;align-content:start}.address strong{font-size:12pt;margin-bottom:4px}.header time{white-space:nowrap}.meta{display:grid;grid-template-columns:repeat(4,1fr);border:1px solid #202020;margin:8mm 0 5mm}.meta div{display:grid;gap:4px;padding:6px 8px;border-right:1px solid #202020;font-size:8pt}.meta div:last-child{border-right:0}.meta span{color:#555}.meta strong{font-size:9pt}table{width:100%;border-collapse:collapse;table-layout:fixed;font-size:8.5pt}th,td{border:1px solid #202020;padding:5px 4px;word-break:break-word;vertical-align:middle}th{background:#d0d0d0;font-size:8.5pt}th:nth-child(1){width:12%}th:nth-child(2){width:11%}th:nth-child(3){width:14%}th:nth-child(4){width:19%}th:nth-child(5){width:22%}th:nth-child(6){width:22%}</style></head><body><main class="sheet"><header class="header"><div class="address"><strong>${escapeHtml(dispatchResult.customer.name)}</strong>${address}</div><time>${new Date().toLocaleDateString("en-GB", { day: "2-digit", month: "short", year: "2-digit" })}</time></header><section class="meta"><div><span>Invoice No.</span><strong>${escapeHtml(dispatchResult.invoiceNumber)}</strong></div><div><span>SO No.</span><strong>${escapeHtml(dispatchResult.salesOrderNumber)}</strong></div><div><span>Dispatch date</span><strong>${escapeHtml(dispatchResult.dispatchDate)}</strong></div><div><span>Boxes</span><strong>${dispatchResult.dispatched}</strong></div></section><table><thead><tr><th>Invoice No.</th><th>SO No.</th><th>Dispatch Date</th><th>Batch No.</th><th>Item</th><th>Box No.</th></tr></thead><tbody>${htmlRows}</tbody></table></main><script>window.onload=()=>window.print();<\/script></body></html>`;
    };

    await printDocumentToServer({
      title: `Sales Order - ${dispatchResult.salesOrderNumber}`,
      subtitle: `Invoice: ${dispatchResult.invoiceNumber} | Dispatch Date: ${dispatchResult.dispatchDate}`,
      address: [dispatchResult.customer.name, ...addressLines],
      headers,
      rows,
    }, fallbackHtml);
  }

  async function downloadPackingListPdf() {
    if (!dispatchResult) return;
    const pdfDocument = await PDFDocument.create();
    const regular = await pdfDocument.embedFont(StandardFonts.Helvetica);
    const bold = await pdfDocument.embedFont(StandardFonts.HelveticaBold);
    const pageWidth = 595.28;
    const pageHeight = 841.89;
    const margin = 36;
    const columns = [64, 58, 72, 88, 126, 115];
    const headers = ["Invoice No.", "SO No.", "Dispatch Date", "Batch No.", "Item", "Box No."];
    const generatedDate = new Date().toLocaleDateString("en-GB", { day: "2-digit", month: "short", year: "2-digit" });
    const wrap = (value: string, font: typeof regular, size: number, width: number) => {
      const words = value.split(/\s+/).filter(Boolean); const lines: string[] = []; let line = "";
      for (const word of words) { const next = line ? `${line} ${word}` : word; if (font.widthOfTextAtSize(next, size) <= width || !line) line = next; else { lines.push(line); line = word; } }
      return lines.length || line ? [...lines, line] : [""];
    };
    const drawHeader = (page: ReturnType<typeof pdfDocument.addPage>) => {
      page.drawRectangle({ x: margin, y: pageHeight - margin - 112, width: pageWidth - margin * 2, height: 112, color: rgb(0.1, 0.2, 0.17) });
      let y = pageHeight - margin - 28;
      page.drawText(dispatchResult.customer.name, { x: margin + 18, y, size: 11, font: bold, color: rgb(1, 1, 1) }); y -= 18;
      for (const line of packingAddressLines(dispatchResult.customer)) { page.drawText(line, { x: margin + 18, y, size: 9, font: regular, color: rgb(1, 1, 1) }); y -= 15; }
      page.drawText(generatedDate, { x: pageWidth - margin - 80, y: pageHeight - margin - 70, size: 10, font: regular, color: rgb(1, 1, 1) });
      const metaY = pageHeight - margin - 145; const meta = [["Invoice No.", dispatchResult.invoiceNumber], ["SO No.", dispatchResult.salesOrderNumber], ["Dispatch date", dispatchResult.dispatchDate], ["Boxes", String(dispatchResult.dispatched)]];
      const metaWidth = (pageWidth - margin * 2) / 4;
      meta.forEach(([label, value], index) => { const x = margin + index * metaWidth; page.drawRectangle({ x, y: metaY - 34, width: metaWidth, height: 34, borderColor: rgb(0.13, 0.13, 0.13), borderWidth: 0.75 }); page.drawText(label, { x: x + 5, y: metaY - 11, size: 6.5, font: regular }); page.drawText(value, { x: x + 5, y: metaY - 25, size: 8, font: bold }); });
      return metaY - 52;
    };
    const drawTableHeader = (page: ReturnType<typeof pdfDocument.addPage>, y: number) => { let x = margin; headers.forEach((header, index) => { page.drawRectangle({ x, y: y - 18, width: columns[index], height: 18, color: rgb(0.8, 0.8, 0.8), borderColor: rgb(0.13, 0.13, 0.13), borderWidth: 0.75 }); page.drawText(header, { x: x + 3, y: y - 12, size: 6.5, font: bold }); x += columns[index]; }); return y - 18; };
    let page = pdfDocument.addPage([pageWidth, pageHeight]);
    let y = drawTableHeader(page, drawHeader(page));
    for (const label of dispatchResult.labels) {
      const cells = [dispatchResult.invoiceNumber, dispatchResult.salesOrderNumber, dispatchResult.dispatchDate, label.batchNumber, label.itemName, label.barcodeValue].map((value, index) => wrap(value, regular, 7, columns[index] - 6));
      const rowHeight = Math.max(18, Math.max(...cells.map((lines) => lines.length)) * 10 + 7);
      if (y - rowHeight < margin) { page = pdfDocument.addPage([pageWidth, pageHeight]); y = drawTableHeader(page, drawHeader(page)); }
      let x = margin;
      cells.forEach((lines, index) => { page.drawRectangle({ x, y: y - rowHeight, width: columns[index], height: rowHeight, borderColor: rgb(0.13, 0.13, 0.13), borderWidth: 0.75 }); lines.forEach((line, lineIndex) => page.drawText(line, { x: x + 3, y: y - 10 - lineIndex * 10, size: 7, font: regular })); x += columns[index]; });
      y -= rowHeight;
    }
    const bytes = await pdfDocument.save();
    const blob = new Blob([bytes], { type: "application/pdf" });
    const link = document.createElement("a"); link.href = URL.createObjectURL(blob); link.download = `packing-list-${dispatchResult.salesOrderNumber}.pdf`; link.click(); URL.revokeObjectURL(link.href);
  }

  function packingAddressLines(customer: DispatchResult["customer"]) {
    return [customer.address1, customer.address2, [customer.city, customer.state, customer.pincode].filter(Boolean).join(" "), customer.country].filter(Boolean);
  }

  async function loadReport(path: string) {
    if (!apiToken) { notify("Sign in with the API online before loading reports."); return null; }
    const response = await fetch(`${apiUrl}/api/reports/${path}`, { headers: { Authorization: `Bearer ${apiToken}` } });
    if (!response.ok) { notify("The report could not be loaded."); return null; }
    return response.json();
  }

  async function loadDailyPrintReport() { const rows = await loadReport(`daily-print?date=${reportDate}`); if (rows) setDailyPrintRows(rows as DailyPrintRow[]); }
  async function loadStockReport() { const query = new URLSearchParams(); if (reportDepartmentId) query.set("departmentId", reportDepartmentId); if (reportItemId) query.set("itemId", reportItemId); const rows = await loadReport(`stock-summary?${query}`); if (rows) setStockReportRows(rows as StockSummaryRow[]); }
  async function loadItemDetailReport() { if (!reportBarcode.trim()) { notify("Enter a full barcode or its serial ending."); return; } const row = await loadReport(`item-detail?barcode=${encodeURIComponent(reportBarcode.trim())}`); if (row) setItemDetailReport(row as ItemDetailRow); else { setItemDetailReport(null); notify("No label matches that barcode or serial ending."); } }
  async function loadBatchReport() { if (!reportBatch.trim()) { notify("Enter a batch number."); return; } const rows = await loadReport(`batch?batchNumber=${encodeURIComponent(reportBatch.trim())}`); if (rows) setBatchReportRows(rows as BatchRow[]); }
  async function loadCustomerReport() { if (!reportCustomerId) { notify("Select a customer."); return; } const result = await loadReport(`customer/${reportCustomerId}`); if (result) setCustomerReport(result as CustomerReport); }
  async function loadInventorySummary(kind: "batch-wise" | "product-wise") { const query = new URLSearchParams(); if (kind === "batch-wise" && reportBatch.trim()) query.set("batchNumber", reportBatch.trim()); if (kind === "product-wise" && reportItemId) query.set("itemId", reportItemId); const rows = await loadReport(`${kind}?${query}`); if (rows) setInventorySummaryRows(rows as InventorySummaryRow[]); }
  async function loadSalesOrderSummary() { if (!reportSalesOrder.trim()) { notify("Enter a sales order number."); return; } const rows = await loadReport(`sales-order-summary?salesOrderNumber=${encodeURIComponent(reportSalesOrder.trim())}`); if (rows) setSalesOrderSummaryRows(rows as SalesOrderSummaryRow[]); }
  async function loadBoxDetails() { const query = new URLSearchParams(); if (reportDepartmentId) query.set("departmentId", reportDepartmentId); if (reportItemId) query.set("itemId", reportItemId); if (reportFromBarcode) query.set("fromBarcode", reportFromBarcode); if (reportToBarcode) query.set("toBarcode", reportToBarcode); const rows = await loadReport(`box-details?${query}`); if (rows) setBoxDetailRows(rows as BoxDetailRow[]); }
  function exportReport(rows: string[][], headers: string[], fileName: string, format: "csv" | "xls" | "xlsx" = "csv") { const workbook = XLSX.utils.book_new(); XLSX.utils.book_append_sheet(workbook, XLSX.utils.aoa_to_sheet([headers, ...rows]), "Report"); XLSX.writeFile(workbook, `${fileName}.${format}`, { bookType: format }); }

  function openReportPrint(document: ReportPrintDocument) { if (!document.rows.length) { notify("Load report data before printing."); return; } setReportPrintDocument(document); }
  async function printReportDocument() {
    if (!reportPrintDocument) return;
    const escapeHtml = (value: string) => value.replace(/[&<>"']/g, (character) => ({ "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;", "'": "&#39;" }[character] ?? character));
    const fallbackHtml = () => {
      const head = reportPrintDocument.headers.map((header) => `<th>${escapeHtml(header)}</th>`).join("");
      const rows = reportPrintDocument.rows.map((row) => `<tr>${row.map((cell) => `<td>${escapeHtml(cell)}</td>`).join("")}</tr>`).join("");
      const address = reportPrintDocument.address?.map((line) => `<span>${escapeHtml(line)}</span>`).join("") ?? "";
      return `<!doctype html><html><head><title>${escapeHtml(reportPrintDocument.title)}</title><style>@page{size:A4 portrait;margin:0}*{box-sizing:border-box}body{margin:0;font-family:Arial,sans-serif;color:#111}.sheet{width:210mm;min-height:297mm;padding:14mm 12mm}.date{text-align:right;font-size:10pt}.title{text-align:center;font-size:17pt;text-decoration:underline;margin:9mm 0 4mm}.subtitle{text-align:center;margin:0 0 7mm;font-size:10pt}.address{display:grid;gap:4px;margin:0 0 8mm;font-size:10pt;line-height:1.35}.address strong{font-size:11pt}table{width:100%;border-collapse:collapse;table-layout:fixed;font-size:8.5pt}th,td{border:1px solid #111;padding:6px 5px;vertical-align:top;word-break:break-word}th{background:#d0d0d0;font-weight:700}</style></head><body><main class="sheet"><div class="date">${new Date().toLocaleDateString("en-GB", { day: "2-digit", month: "short", year: "2-digit" })}</div><h1 class="title">${escapeHtml(reportPrintDocument.title)}</h1>${reportPrintDocument.subtitle ? `<p class="subtitle">${escapeHtml(reportPrintDocument.subtitle)}</p>` : ""}${address ? `<section class="address">${address}</section>` : ""}<table><thead><tr>${head}</tr></thead><tbody>${rows}</tbody></table></main><script>window.onload=()=>window.print();<\/script></body></html>`;
    };

    await printDocumentToServer({
      title: reportPrintDocument.title,
      subtitle: reportPrintDocument.subtitle,
      address: reportPrintDocument.address,
      headers: reportPrintDocument.headers,
      rows: reportPrintDocument.rows,
    }, fallbackHtml);
  }
  async function downloadReportPdf() { if (!reportPrintDocument) return; const pdf = await PDFDocument.create(); const regular = await pdf.embedFont(StandardFonts.Helvetica); const bold = await pdf.embedFont(StandardFonts.HelveticaBold); const width = 595.28; const height = 841.89; const margin = 36; let page = pdf.addPage([width, height]); let y = height - margin; const drawPageHeader = () => { page.drawText(new Date().toLocaleDateString("en-GB", { day: "2-digit", month: "short", year: "2-digit" }), { x: width - margin - 76, y, size: 8, font: regular }); y -= 28; page.drawText(reportPrintDocument.title, { x: margin, y, size: 15, font: bold }); y -= 19; if (reportPrintDocument.subtitle) { page.drawText(reportPrintDocument.subtitle, { x: margin, y, size: 9, font: regular }); y -= 16; } for (const line of reportPrintDocument.address ?? []) { page.drawText(line, { x: margin, y, size: 9, font: regular }); y -= 14; } y -= 8; }; drawPageHeader(); const columnWidth = (width - margin * 2) / reportPrintDocument.headers.length; const drawHeader = () => { let x = margin; reportPrintDocument.headers.forEach((header) => { page.drawRectangle({ x, y: y - 22, width: columnWidth, height: 22, color: rgb(.82, .82, .82), borderColor: rgb(0, 0, 0), borderWidth: .7 }); page.drawText(header.slice(0, 22), { x: x + 3, y: y - 13, size: 6.5, font: bold }); x += columnWidth; }); y -= 22; }; drawHeader(); reportPrintDocument.rows.forEach((row) => { if (y < margin + 28) { page = pdf.addPage([width, height]); y = height - margin; drawPageHeader(); drawHeader(); } let x = margin; row.forEach((cell) => { page.drawRectangle({ x, y: y - 20, width: columnWidth, height: 20, borderColor: rgb(0, 0, 0), borderWidth: .7 }); page.drawText(cell.slice(0, 24), { x: x + 3, y: y - 13, size: 6.5, font: regular }); x += columnWidth; }); y -= 20; }); const bytes = await pdf.save(); const link = document.createElement("a"); link.href = URL.createObjectURL(new Blob([bytes], { type: "application/pdf" })); link.download = `${reportPrintDocument.title.toLowerCase().replace(/[^a-z0-9]+/g, "-")}.pdf`; link.click(); URL.revokeObjectURL(link.href); }

  async function printPackingListSummary() {
    if (!dispatchResult) return;
    const escapeHtml = (value: string) => value.replace(/[&<>"']/g, (character) => ({ "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;", "'": "&#39;" }[character] ?? character));
    const addressLines = packingAddressLines(dispatchResult.customer);
    const headers = ["Sr. No.", "Item", "Pcs in Each Box", "Total No. of Boxes", "Total No. of Pcs", "Gross Wt of One box (kgs)", "Total Gross wt. (kgs)", "Remark"];
    const rows = packingListLines.map((line, index) => [
      String(index + 1),
      line.itemName,
      String(line.piecesPerBox),
      String(line.boxes),
      String(line.totalPieces),
      line.grossWeightKg.toFixed(2),
      line.totalGrossWeightKg.toFixed(2),
      "",
    ]);

    const fallbackHtml = () => {
      const address = addressLines.map((line) => `<span>${escapeHtml(line)}</span>`).join("");
      const htmlRows = rows.map((r) => `<tr>${r.map((c) => `<td>${escapeHtml(c)}</td>`).join("")}</tr>`).join("");
      return `<!doctype html><html><head><title>Packing List ${escapeHtml(dispatchResult.salesOrderNumber)}</title><style>@page{size:A4 portrait;margin:0}*{box-sizing:border-box}body{margin:0;color:#111;font-family:Arial,sans-serif}.sheet{width:210mm;min-height:297mm;padding:14mm 12mm}.title{text-align:center;text-decoration:underline;font-size:20pt;margin:0 0 20mm}.so-date{display:flex;justify-content:space-between;font-size:11pt;margin-bottom:7mm}.address{display:grid;gap:5px;font-size:10pt;line-height:1.4;margin-bottom:8mm}.address strong{text-decoration:underline;font-size:11pt}table{width:100%;border-collapse:collapse;table-layout:fixed;font-size:8.5pt}th,td{border:1px solid #111;padding:6px 4px;text-align:center;vertical-align:middle;word-break:break-word}th{font-weight:700}th:nth-child(1){width:7%}th:nth-child(2){width:25%}th:nth-child(3){width:10%}th:nth-child(4){width:12%}th:nth-child(5){width:12%}th:nth-child(6){width:12%}th:nth-child(7){width:12%}th:nth-child(8){width:10%}</style></head><body><main class="sheet"><h1 class="title">Packing List</h1><div class="so-date"><strong>SO No. : ${escapeHtml(dispatchResult.salesOrderNumber)}</strong><span>${new Date().toLocaleDateString("en-GB", { day: "2-digit", month: "short", year: "2-digit" })}</span></div><section class="address"><strong>Ship To Address</strong><b>${escapeHtml(dispatchResult.customer.name)}</b>${address}</section><table><thead><tr><th>Sr. No.</th><th>Item</th><th>Pcs in Each Box</th><th>Total No. of Boxes</th><th>Total No. of Pcs</th><th>Gross Wt of One box in (kgs)</th><th>Total Gross wt. (kgs)</th><th>Remark</th></tr></thead><tbody>${htmlRows}</tbody></table></main><script>window.onload=()=>window.print();<\/script></body></html>`;
    };

    await printDocumentToServer({
      title: `Packing List - SO ${dispatchResult.salesOrderNumber}`,
      subtitle: `Date: ${new Date().toLocaleDateString("en-GB", { day: "2-digit", month: "short", year: "2-digit" })}`,
      address: [dispatchResult.customer.name, ...addressLines],
      headers,
      rows,
    }, fallbackHtml);
  }

  async function downloadPackingListSummaryPdf() {
    if (!dispatchResult) return;
    const pdfDocument = await PDFDocument.create();
    const regular = await pdfDocument.embedFont(StandardFonts.Helvetica);
    const bold = await pdfDocument.embedFont(StandardFonts.HelveticaBold);
    const pageWidth = 595.28; const pageHeight = 841.89; const margin = 36;
    const columns = [32, 124, 48, 62, 62, 72, 72, 50];
    const headers = ["Sr. No.", "Item", "Pcs in Each Box", "Total No. of Boxes", "Total No. of Pcs", "Gross Wt of One box (kgs)", "Total Gross wt. (kgs)", "Remark"];
    const page = pdfDocument.addPage([pageWidth, pageHeight]);
    page.drawText("Packing List", { x: 244, y: 790, size: 17, font: bold });
    page.drawLine({ start: { x: 244, y: 787 }, end: { x: 346, y: 787 }, thickness: 0.8 });
    page.drawText(`SO No. : ${dispatchResult.salesOrderNumber}`, { x: margin, y: 746, size: 10, font: bold });
    page.drawText(new Date().toLocaleDateString("en-GB", { day: "2-digit", month: "short", year: "2-digit" }), { x: 500, y: 746, size: 10, font: regular });
    let addressY = 713; page.drawText("Ship To Address", { x: margin, y: addressY, size: 10, font: bold }); addressY -= 19;
    page.drawText(dispatchResult.customer.name, { x: margin, y: addressY, size: 10, font: bold }); addressY -= 15;
    for (const line of packingAddressLines(dispatchResult.customer)) { page.drawText(line, { x: margin, y: addressY, size: 9, font: regular }); addressY -= 14; }
    let y = addressY - 12; let x = margin;
    headers.forEach((header, index) => { page.drawRectangle({ x, y: y - 34, width: columns[index], height: 34, borderColor: rgb(0, 0, 0), borderWidth: 0.8 }); const words = header.split(" "); let line = ""; let lineY = y - 11; for (const word of words) { const next = line ? `${line} ${word}` : word; if (bold.widthOfTextAtSize(next, 6.3) > columns[index] - 5 && line) { page.drawText(line, { x: x + 3, y: lineY, size: 6.3, font: bold }); line = word; lineY -= 8; } else line = next; } page.drawText(line, { x: x + 3, y: lineY, size: 6.3, font: bold }); x += columns[index]; }); y -= 34;
    packingListLines.forEach((line, index) => { const values = [String(index + 1), line.itemName, String(line.piecesPerBox), String(line.boxes), String(line.totalPieces), line.grossWeightKg.toFixed(2), line.totalGrossWeightKg.toFixed(2), ""]; x = margin; values.forEach((value, cellIndex) => { page.drawRectangle({ x, y: y - 24, width: columns[cellIndex], height: 24, borderColor: rgb(0, 0, 0), borderWidth: 0.8 }); page.drawText(value.slice(0, cellIndex === 1 ? 26 : 18), { x: x + 3, y: y - 15, size: 7, font: regular }); x += columns[cellIndex]; }); y -= 24; });
    const bytes = await pdfDocument.save(); const blob = new Blob([bytes], { type: "application/pdf" }); const link = document.createElement("a"); link.href = URL.createObjectURL(blob); link.download = `packing-list-${dispatchResult.salesOrderNumber}.pdf`; link.click(); URL.revokeObjectURL(link.href);
  }

  function departmentName(id: number) {
    return (
      departments.find((department) => department.id === id)?.name ?? "Unknown"
    );
  }

  if (!loggedIn)
    return (
      <main className="login-page">
        <section className="login-panel">
          <p className="eyebrow">BOXTRACK / VAPI</p>
          <h1>Inventory with a memory.</h1>
          <p className="muted">
            Sign in to manage departments, items, and customers.
          </p>
          <form onSubmit={login}>
            <label>
              Username
              <input
                value={userName}
                onChange={(event) => setUserName(event.target.value)}
                autoComplete="username"
              />
            </label>
            <label>
              Password
              <input
                type="password"
                value={password}
                onChange={(event) => setPassword(event.target.value)}
                autoComplete="current-password"
              />
            </label>
            {loginError && <p className="error">{loginError}</p>}
            <button type="submit">
              Sign in <span>-&gt;</span>
            </button>
          </form>
        </section>
        <aside className="login-aside">
          <div className="barcode-lines" />
          <p>Individual cartons. Traceable stock. Clear dispatch.</p>
        </aside>
      </main>
    );

  return (
    <main className="app-shell">
      <header>
        <div className="brand">
          <span className="brand-mark">B</span>
          <div>
            <strong>BOXTRACK</strong>
            <small>VAPI / OPERATIONS</small>
          </div>
        </div>
        <div className="status">
          <span /> {currentUserRole === "Production" ? `Production — ${currentUserDeptName ?? currentUsername}` : currentUserRole === "QC" ? "QC Panel" : "Admin session"}{" "}
          <button
            className="link-button"
            onClick={() => {
              setApiToken("");
              setLoggedIn(false);
              setUserName("");
              setPassword("");
              setCurrentUserRole("Admin");
              setCurrentUserDeptId(null);
              setCurrentUserDeptName(null);
              setCurrentUsername("");
              setActive("Master");
            }}
          >
            Sign out
          </button>
        </div>
      </header>
      <nav>
        {roleMenu.map((item) => (
          <button
            key={item}
            className={active === item ? "active" : ""}
            onClick={() => setActive(item)}
          >
            {item}
          </button>
        ))}
      </nav>
      <section className="content">
        {active === "Master" ? (
          <>
            <div className="content-heading">
              <div>
                <p className="eyebrow">CONTROL DESK / MASTER</p>
                <h1>Master data</h1>
              </div>
              <span className="date">06 OCT 2026</span>
            </div>
            <div className="workspace">
              <div className="workspace-tabs">
                <button
                  className={masterTab === "departments" ? "selected" : ""}
                  onClick={() => setMasterTab("departments")}
                >
                  Department master
                </button>
                <button
                  className={masterTab === "items" ? "selected" : ""}
                  onClick={() => setMasterTab("items")}
                >
                  Item master
                </button>
              </div>
              {masterTab === "departments" ? (
                <section className="master-panel">
                  <form
                    className="master-form department-form"
                    onSubmit={saveDepartment}
                  >
                    <div>
                      <p className="form-kicker">DEPARTMENT RECORD</p>
                      <h2>
                        {departmentDraft.id
                          ? "Edit department"
                          : "New department"}
                      </h2>
                    </div>
                    <label>
                      Department ID
                      <input value={departmentDraft.id || "Auto"} readOnly />
                    </label>
                    <label>
                      Department name<span className="required">*</span>
                      <input
                        value={departmentDraft.name}
                        onChange={(event) =>
                          setDepartmentDraft({
                            ...departmentDraft,
                            name: event.target.value,
                          })
                        }
                        placeholder="e.g. AFC"
                        autoFocus
                      />
                    </label>
                    <div className="form-actions">
                      <button type="submit" className="primary">
                        {departmentDraft.id ? "Update" : "Save"}{" "}
                        <span>-&gt;</span>
                      </button>
                      <button
                        type="button"
                        className="secondary"
                        onClick={resetDepartment}
                      >
                        Cancel
                      </button>
                    </div>
                  </form>
                  <div className="table-toolbar">
                    <div>
                      <h2>Departments</h2>
                      <p>{departments.length} active records</p>
                    </div>
                    <input
                      value={departmentSearch}
                      onChange={(event) =>
                        setDepartmentSearch(event.target.value)
                      }
                      placeholder="Search departments"
                    />
                  </div>
                  <div className="table-wrap">
                    <table>
                      <thead>
                        <tr>
                          <th>Sr. No.</th>
                          <th>Department name</th>
                          <th className="actions-heading">Actions</th>
                        </tr>
                      </thead>
                      <tbody>
                        {filteredDepartments.map((department, index) => (
                          <tr key={department.id}>
                            <td>{index + 1}</td>
                            <td className="strong-cell">{department.name}</td>
                            <td className="row-actions">
                              <button
                                onClick={() => setDepartmentDraft(department)}
                              >
                                Edit
                              </button>
                              <button
                                onClick={() => deleteDepartment(department.id)}
                              >
                                Delete
                              </button>
                            </td>
                          </tr>
                        ))}
                      </tbody>
                    </table>
                  </div>
                </section>
              ) : (
                <section className="master-panel">
                  <form className="master-form item-form" onSubmit={saveItem}>
                    <div>
                      <p className="form-kicker">ITEM RECORD</p>
                      <h2>{itemDraft.id ? "Edit item" : "New item"}</h2>
                    </div>
                    <label>
                      ID Code
                      <input
                        value={
                          itemDraft.id
                            ? items.find((item) => item.id === itemDraft.id)
                                ?.code
                            : nextCode
                        }
                        readOnly
                      />
                    </label>
                    <label>
                      Item name<span className="required">*</span>
                      <input
                        value={itemDraft.name}
                        onChange={(event) =>
                          setItemDraft({
                            ...itemDraft,
                            name: event.target.value,
                          })
                        }
                        placeholder="e.g. 100 ML REGULAR"
                      />
                    </label>
                    <label className="wide-field">
                      Description
                      <textarea
                        value={itemDraft.description}
                        onChange={(event) =>
                          setItemDraft({
                            ...itemDraft,
                            description: event.target.value,
                          })
                        }
                        placeholder="Product description"
                      />
                    </label>
                    <label>
                      Packaging per box<span className="required">*</span>
                      <input
                        type="number"
                        min="1"
                        step="1"
                        value={itemDraft.packing}
                        onChange={(event) =>
                          setItemDraft({
                            ...itemDraft,
                            packing: event.target.value,
                          })
                        }
                        placeholder="5000"
                      />
                    </label>
                    <label>
                      Department<span className="required">*</span>
                      <select
                        value={itemDraft.departmentId}
                        onChange={(event) =>
                          setItemDraft({
                            ...itemDraft,
                            departmentId: event.target.value,
                          })
                        }
                      >
                        {departments.map((department) => (
                          <option key={department.id} value={department.id}>
                            {department.name}
                          </option>
                        ))}
                      </select>
                    </label>
                    <label>
                      Gross weight (kg)<span className="required">*</span>
                      <input
                        type="number"
                        min="0.01"
                        step="0.01"
                        value={itemDraft.weight}
                        onChange={(event) =>
                          setItemDraft({
                            ...itemDraft,
                            weight: event.target.value,
                          })
                        }
                        placeholder="7.20"
                      />
                    </label>
                    <div className="form-actions item-actions">
                      <button type="submit" className="primary">
                        {itemDraft.id ? "Update" : "Save"} <span>-&gt;</span>
                      </button>
                      <button
                        type="button"
                        className="secondary"
                        onClick={resetItem}
                      >
                        Cancel
                      </button>
                    </div>
                  </form>
                  <div className="table-toolbar item-toolbar">
                    <div>
                      <h2>Items</h2>
                      <p>
                        {filteredItems.length} of {items.length} active records
                      </p>
                    </div>
                    <div className="toolbar-actions">
                      <input
                        value={itemSearch.name}
                        onChange={(event) =>
                          setItemSearch({
                            ...itemSearch,
                            name: event.target.value,
                          })
                        }
                        placeholder="Search item name"
                      />
                      <select
                        value={itemSearch.departmentId}
                        onChange={(event) =>
                          setItemSearch({
                            ...itemSearch,
                            departmentId: event.target.value,
                          })
                        }
                      >
                        <option value="">All departments</option>
                        {departments.map((department) => (
                          <option key={department.id} value={department.id}>
                            {department.name}
                          </option>
                        ))}
                      </select>
                      <input
                        ref={importRef}
                        type="file"
                        accept=".csv,.xls,.xlsx"
                        onChange={importItems}
                        hidden
                      />
                      <button
                        className="secondary"
                        onClick={() => importRef.current?.click()}
                      >
                        Import
                      </button>
                      <button className="secondary" onClick={exportItems}>
                        Export CSV
                      </button>
                    </div>
                  </div>
                  <div className="table-wrap">
                    <table>
                      <thead>
                        <tr>
                          <th>Sr. No.</th>
                          <th>Item ID</th>
                          <th>Item name</th>
                          <th>Description</th>
                          <th>Department</th>
                          <th>Packing</th>
                          <th>Gross weight</th>
                          <th className="actions-heading">Actions</th>
                        </tr>
                      </thead>
                      <tbody>
                        {filteredItems.map((item, index) => (
                          <tr key={item.id}>
                            <td>{index + 1}</td>
                            <td className="code-cell">{item.code}</td>
                            <td className="strong-cell">{item.name}</td>
                            <td>{item.description}</td>
                            <td>{departmentName(item.departmentId)}</td>
                            <td>{item.packing}</td>
                            <td>{item.weight.toFixed(2)}</td>
                            <td className="row-actions">
                              <button
                                onClick={() =>
                                  setItemDraft({
                                    id: item.id,
                                    name: item.name,
                                    description: item.description,
                                    packing: String(item.packing),
                                    departmentId: String(item.departmentId),
                                    weight: String(item.weight),
                                  })
                                }
                              >
                                Edit
                              </button>
                              <button onClick={() => deleteItem(item.id)}>
                                Delete
                              </button>
                            </td>
                          </tr>
                        ))}
                      </tbody>
                    </table>
                    {filteredItems.length === 0 && (
                      <p className="no-results">No matching items.</p>
                    )}
                  </div>
                </section>
              )}
            </div>
          </>
        ) : active === "Customer" ? (
          <>
            <div className="content-heading"><div><p className="eyebrow">CONTROL DESK / CUSTOMER</p><h1>Customer master</h1></div><span className="date">06 OCT 2026</span></div>
            <div className="workspace"><section className="master-panel customer-panel">
              <form className="master-form customer-form" onSubmit={saveCustomer}>
                <div><p className="form-kicker">CUSTOMER RECORD</p><h2>{customerDraft.id ? "Edit customer" : "New customer"}</h2></div>
                <label>Customer ID<input value={customerDraft.id || "Auto"} readOnly /></label>
                <label>Legacy C_ID<input type="number" min="1" value={customerDraft.legacyId ?? ""} onChange={(event) => setCustomerDraft({ ...customerDraft, legacyId: event.target.value ? Number(event.target.value) : undefined })} placeholder="Optional" /></label>
                <label className="wide-field">Name<span className="required">*</span><input value={customerDraft.name} onChange={(event) => setCustomerDraft({ ...customerDraft, name: event.target.value })} placeholder="Customer name" autoFocus /></label>
                <label className="wide-field">Address 1<input value={customerDraft.address1} onChange={(event) => setCustomerDraft({ ...customerDraft, address1: event.target.value })} /></label>
                <label className="wide-field">Address 2<input value={customerDraft.address2} onChange={(event) => setCustomerDraft({ ...customerDraft, address2: event.target.value })} /></label>
                <label>City<input list="customer-city-options" value={customerDraft.city} onChange={(event) => setCustomerDraft({ ...customerDraft, city: event.target.value })} /><datalist id="customer-city-options">{cityOptions.map((city) => <option key={city} value={city} />)}</datalist></label>
                <label>Pincode<input inputMode="numeric" maxLength={6} value={customerDraft.pincode} onChange={(event) => setCustomerDraft({ ...customerDraft, pincode: event.target.value.replace(/\D/g, "") })} placeholder="6 digits" /></label>
                <label>State<select value={customerDraft.state} onChange={(event) => setCustomerDraft({ ...customerDraft, state: event.target.value })}><option value="">Select state</option>{indianStates.map((state) => <option key={state}>{state}</option>)}</select></label>
                <label>Country<select value={customerDraft.country} onChange={(event) => setCustomerDraft({ ...customerDraft, country: event.target.value })}><option value="">Select country</option>{countries.map((country) => <option key={country}>{country}</option>)}</select></label>
                <div className="form-actions customer-actions"><button type="submit" className="primary">{customerDraft.id ? "Update" : "Save"} <span>-&gt;</span></button><button type="button" className="secondary" onClick={resetCustomer}>Cancel</button></div>
              </form>
              <div className="table-toolbar item-toolbar"><div><h2>Customers</h2><p>{filteredCustomers.length} of {customers.length} active records</p></div><div className="toolbar-actions"><input value={customerSearch} onChange={(event) => setCustomerSearch(event.target.value)} placeholder="Search customer name" /><input ref={customerImportRef} type="file" accept=".csv,.xls,.xlsx" onChange={importCustomers} hidden /><button className="secondary" onClick={() => customerImportRef.current?.click()}>Import</button><button className="secondary" onClick={() => exportCustomers("csv")}>Export CSV</button><button className="secondary" onClick={() => exportCustomers("xls")}>Export XLS</button><button className="secondary" onClick={() => exportCustomers("xlsx")}>Export XLSX</button></div></div>
              <div className="customer-preview table-wrap"><table><thead><tr><th>Sr. No.</th><th>C_ID</th><th>C_Name</th><th>C_Add1</th><th>C_Add2</th><th>C_City</th><th>C_Pin</th><th>C_State</th><th>C_Country</th><th className="actions-heading">Actions</th></tr></thead><tbody>{filteredCustomers.map((customer, index) => <tr key={customer.id}><td>{index + 1}</td><td className="code-cell">{customer.legacyId ?? customer.id}</td><td className="strong-cell">{customer.name}</td><td>{customer.address1}</td><td>{customer.address2}</td><td>{customer.city}</td><td>{customer.pincode}</td><td>{customer.state}</td><td>{customer.country}</td><td className="row-actions"><button onClick={() => setCustomerDraft(customer)}>Edit</button><button onClick={() => deleteCustomer(customer.id)}>Delete</button></td></tr>)}</tbody></table>{filteredCustomers.length === 0 && <p className="no-results">No matching customers.</p>}</div>
            </section></div>
          </>
        ) : active === "Dispatch" ? (
          <>
            <div className="content-heading"><div><p className="eyebrow">CONTROL DESK / DISPATCH</p><h1>Manage dispatch</h1></div><span className="date">07 OCT 2026</span></div>
            <div className="workspace">
              <div className="workspace-tabs">
                <button className={dispatchTab === "manage" ? "selected" : ""} onClick={() => setDispatchTab("manage")}>Manage dispatch</button>
                <button className={dispatchTab === "history" ? "selected" : ""} onClick={() => setDispatchTab("history")}>Old dispatch</button>
              </div>
              {dispatchTab === "manage" ? <section className="master-panel dispatch-panel">
              <div className="dispatch-actions"><button type="submit" form="dispatch-form" className="primary">Generate sales order <span>-&gt;</span></button><button type="button" className="secondary" disabled={!dispatchResult} onClick={() => { setDispatchPreviewKind("packing"); setIsPackingPreviewOpen(true); }}>Generate packing list</button><button type="button" className="secondary" disabled title="Device import will be added later">Import Data from Device</button></div>
              <form id="dispatch-form" className="dispatch-form" onSubmit={generateSalesOrder}>
                <div><p className="form-kicker">GENERATE SALES ORDER</p><h2>Confirm scanned dispatch labels</h2></div>
                <label>Sales Order No.<input value={dispatchDraft.salesOrderNumber} onChange={(event) => { setDispatchResult(null); setDispatchDraft({ ...dispatchDraft, salesOrderNumber: event.target.value }); }} placeholder="Enter sales order number" /></label>
                <label>Invoice No.<input value={dispatchDraft.invoiceNumber} onChange={(event) => { setDispatchResult(null); setDispatchDraft({ ...dispatchDraft, invoiceNumber: event.target.value }); }} placeholder="Enter invoice number" /></label>
                <label className="dispatch-customer">Customer<div className="customer-picker"><input value={dispatchDraft.customerSearch} placeholder="Search customer name or ID" role="combobox" aria-expanded={isDispatchCustomerOpen} onFocus={() => setIsDispatchCustomerOpen(true)} onBlur={() => window.setTimeout(() => setIsDispatchCustomerOpen(false), 120)} onChange={(event) => { setDispatchResult(null); setDispatchDraft({ ...dispatchDraft, customerSearch: event.target.value, customerId: "" }); setIsDispatchCustomerOpen(true); }} />{isDispatchCustomerOpen && <div className="customer-picker-options" role="listbox">{dispatchCustomers.length > 0 ? dispatchCustomers.map((customer) => <button key={customer.id} type="button" role="option" onMouseDown={(event) => event.preventDefault()} onClick={() => { setDispatchResult(null); setDispatchDraft({ ...dispatchDraft, customerId: String(customer.id), customerSearch: `${customer.legacyId ?? customer.id} - ${customer.name}` }); setIsDispatchCustomerOpen(false); }}><strong>{customer.legacyId ?? customer.id}</strong><span>{customer.name}</span><small>{customer.city || ""}</small></button>) : <p>No matching customers.</p>}</div>}</div></label>
                <label>Dispatch Date<input type="date" value={dispatchDraft.dispatchDate} onChange={(event) => { setDispatchResult(null); setDispatchDraft({ ...dispatchDraft, dispatchDate: event.target.value }); }} /></label>
                <label className="dispatch-file">Select scanner file<input type="file" accept=".txt,.csv,.xls,.xlsx,text/plain,text/csv,application/vnd.ms-excel,application/vnd.openxmlformats-officedocument.spreadsheetml.sheet" onChange={(event) => { setDispatchResult(null); setDispatchDraft({ ...dispatchDraft, file: event.target.files?.[0] ?? null }); }} /><small>{dispatchDraft.file ? dispatchDraft.file.name : "TXT, CSV, XLS, or XLSX containing barcode numbers"}</small></label>
              </form>
              <section className="dispatch-preview"><div className="table-toolbar"><div><h2>Sales order dispatch</h2><p>{dispatchResult ? `${dispatchResult.dispatched} labels are dispatched to ${dispatchResult.customer.name}. Generate the packing list when you are ready to print.` : "Generate a sales order from the scanned barcode file to confirm dispatch."}</p></div>{dispatchResult && <button type="button" className="secondary" onClick={() => { setDispatchPreviewKind("salesOrder"); setIsPackingPreviewOpen(true); }}>Open sales order preview</button>}</div><div className="table-wrap"><table><thead><tr><th>Sr. No.</th><th>Barcode</th><th>Batch No.</th><th>Item ID</th><th>Item name</th></tr></thead><tbody>{dispatchResult?.labels.map((label, index) => <tr key={label.barcodeValue}><td>{index + 1}</td><td className="code-cell">{label.barcodeValue}</td><td>{label.batchNumber}</td><td>{label.itemCode}</td><td className="strong-cell">{label.itemName}</td></tr>)}</tbody></table>{!dispatchResult && <p className="no-results">No sales order has been generated in this session.</p>}</div></section>
              </section> : <section className="master-panel dispatch-history-panel">
                <div><p className="form-kicker">DISPATCH HISTORY</p><h2>Find past dispatch details</h2><p className="history-copy">Search by sales order, invoice, customer, dispatch date, or a scanned barcode.</p></div>
                <div className="dispatch-history-filters">
                  <label>Sales Order No.<input value={oldDispatchSearch.salesOrderNumber} onChange={(event) => { setOldDispatchSearch({ ...oldDispatchSearch, salesOrderNumber: event.target.value }); setSelectedOldDispatch(null); }} placeholder="Search sales order" /></label>
                  <label>Invoice No.<input value={oldDispatchSearch.invoiceNumber} onChange={(event) => { setOldDispatchSearch({ ...oldDispatchSearch, invoiceNumber: event.target.value }); setSelectedOldDispatch(null); }} placeholder="Search invoice" /></label>
                  <label>Customer<input value={oldDispatchSearch.customer} onChange={(event) => { setOldDispatchSearch({ ...oldDispatchSearch, customer: event.target.value }); setSelectedOldDispatch(null); }} placeholder="Search customer" /></label>
                  <label>Dispatch date<input type="date" value={oldDispatchSearch.dispatchDate} onChange={(event) => { setOldDispatchSearch({ ...oldDispatchSearch, dispatchDate: event.target.value }); setSelectedOldDispatch(null); }} /></label>
                  <label>Barcode<input value={oldDispatchSearch.barcode} onChange={(event) => { setOldDispatchSearch({ ...oldDispatchSearch, barcode: event.target.value.replace(/\D/g, "") }); setSelectedOldDispatch(null); }} placeholder="Search barcode" /></label>
                  <button type="button" className="secondary history-reset" onClick={() => { setOldDispatchSearch({ salesOrderNumber: "", invoiceNumber: "", customer: "", dispatchDate: "", barcode: "" }); setSelectedOldDispatch(null); }}>Clear search</button>
                </div>
                <section className="dispatch-history-results">
                  <div className="table-toolbar"><div><h2>Dispatch records</h2><p>{oldDispatchTotal} matching dispatch record{oldDispatchTotal === 1 ? "" : "s"}</p></div></div>
                  <div className="table-wrap"><table><thead><tr><th>Dispatch ID</th><th>SO No.</th><th>Invoice No.</th><th>Customer</th><th>Dispatch date</th><th>Boxes</th><th>Source file</th><th className="actions-heading">Details</th></tr></thead><tbody>{oldDispatches.map((record) => <tr key={record.dispatchId} className={selectedOldDispatch?.dispatchId === record.dispatchId ? "selected-row" : ""}><td className="code-cell">{record.dispatchId}</td><td>{record.salesOrderNumber}</td><td>{record.invoiceNumber}</td><td className="strong-cell">{record.customer.name}</td><td>{record.dispatchDate}</td><td>{record.labelCount}</td><td>{record.sourceFileName}</td><td className="row-actions"><button type="button" onClick={() => setSelectedOldDispatch(record)}>View</button></td></tr>)}</tbody></table>{oldDispatches.length === 0 && <p className="no-results">No saved dispatch records match these filters.</p>}</div>
                </section>
                {selectedOldDispatch && <section className="old-dispatch-details"><div className="table-toolbar"><div><p className="form-kicker">DISPATCH #{selectedOldDispatch.dispatchId}</p><h2>{selectedOldDispatch.customer.name}</h2><p>{selectedOldDispatch.invoiceNumber} / SO {selectedOldDispatch.salesOrderNumber} / {selectedOldDispatch.dispatchDate}</p></div><button type="button" className="secondary" onClick={() => setSelectedOldDispatch(null)}>Close details</button></div><div className="table-wrap"><table><thead><tr><th>Sr. No.</th><th>Barcode</th><th>Batch No.</th><th>Item ID</th><th>Item name</th></tr></thead><tbody>{selectedOldDispatch.labels.map((label, index) => <tr key={label.barcodeValue}><td>{index + 1}</td><td className="code-cell">{label.barcodeValue}</td><td>{label.batchNumber}</td><td>{label.itemCode}</td><td className="strong-cell">{label.itemName}</td></tr>)}</tbody></table></div></section>}
              </section>}
            </div>
          </>
        ) : active === "Production" ? (
          <>
            <div className="content-heading"><div><p className="eyebrow">CONTROL DESK / PRODUCTION</p><h1>Manage production</h1></div><span className="date">07 OCT 2026</span></div>
            <div className="workspace"><section className="master-panel production-panel">
              <form className="production-form" onSubmit={addLabelsToStock}>
                <div><p className="form-kicker">PENDING LABELS</p><h2>Add completed production to stock</h2></div>
                <label>Department{currentUserRole === "Production" ? <input type="text" readOnly value={currentUserDeptName || (currentUserDeptId ? `Department #${currentUserDeptId}` : "My Department")} style={{ background: "#f3f4f6", cursor: "not-allowed" }} /> : <select value={productionDepartmentId} onChange={(event) => { setProductionDepartmentId(event.target.value); setStockDraft({ ...stockDraft, fromBarcode: "", toBarcode: "" }); }}><option value="">All departments</option>{departments.map((department) => <option key={department.id} value={department.id}>{department.name}</option>)}</select>}</label>
                <label>Batch No.<div className="item-picker"><input value={stockDraft.batchNumber} onChange={(event) => { setStockDraft({ ...stockDraft, batchNumber: event.target.value }); setIsStockBatchOpen(true); }} onFocus={() => setIsStockBatchOpen(true)} onBlur={() => window.setTimeout(() => setIsStockBatchOpen(false), 120)} placeholder="Enter batch number or select old batch" role="combobox" aria-expanded={isStockBatchOpen} />{isStockBatchOpen && <div className="item-picker-options" role="listbox">{(() => { const knownProduction = Array.from(new Set([...batchOptions, ...batchReportRows.map((row) => row.batchNumber), ...inventorySummaryRows.map((row) => row.batchNumber), ...boxDetailRows.map((row) => row.batchNumber)])); const query = stockDraft.batchNumber.trim().toLowerCase(); const filtered = knownProduction.filter((batch) => !query || batch.toLowerCase().includes(query)); return (<>{filtered.map((batch) => <button key={batch} type="button" role="option" onMouseDown={(event) => event.preventDefault()} onClick={() => { setStockDraft({ ...stockDraft, batchNumber: batch }); setIsStockBatchOpen(false); }}><strong>{batch}</strong><span>Batch {batch}</span></button>)}{filtered.length === 0 && <p>{knownProduction.length === 0 ? "No old batches yet - type a new batch number." : "No matching batches - press Add to stock to create it."}</p>}</>); })()}</div>}</div></label>
                <label>Start box<span className="field-hint">Barcode</span><input value={stockDraft.fromBarcode} onChange={(event) => setStockDraft({ ...stockDraft, fromBarcode: event.target.value })} placeholder="Select from list" /></label>
                <label>End box<span className="field-hint">Barcode</span><input value={stockDraft.toBarcode} onChange={(event) => setStockDraft({ ...stockDraft, toBarcode: event.target.value })} placeholder="Select from list" /></label>
                <div className="production-month-filter"><label><input type="checkbox" checked={isProductionMonthFilterOn} onChange={(event) => { setIsProductionMonthFilterOn(event.target.checked); setStockDraft({ ...stockDraft, fromBarcode: "", toBarcode: "" }); }} /> Filter by MFG month</label><input type="month" value={productionMonth} disabled={!isProductionMonthFilterOn} onChange={(event) => { setProductionMonth(event.target.value); setStockDraft({ ...stockDraft, fromBarcode: "", toBarcode: "" }); }} /></div>
                <div className="production-total"><span>Selected labels</span><strong>{productionRangeCount}</strong><small>of {productionTotal} pending</small></div>
                <button type="submit" className="primary production-submit">Add to stock <span>-&gt;</span></button>
              </form>
              <div className="table-toolbar production-toolbar"><div><h2>Pending production labels</h2><p>{productionTotal} label{productionTotal === 1 ? "" : "s"} awaiting production completion</p></div><input value={productionSearch} onChange={(event) => setProductionSearch(event.target.value)} placeholder="Search barcode or item" /></div>
              <div className="table-wrap production-table"><table><thead><tr><th>Barcode</th><th>Item ID</th><th>Item name</th><th>Department</th><th>MFG date</th><th>MFG month</th><th>Serial</th><th className="actions-heading">Range</th></tr></thead><tbody>{pendingProduction.map((label) => <tr key={label.id} className={label.barcodeValue === stockDraft.fromBarcode || label.barcodeValue === stockDraft.toBarcode ? "selected-row" : ""}><td className="code-cell">{label.barcodeValue}</td><td>{label.itemCode}</td><td className="strong-cell">{label.itemName}</td><td>{label.departmentName}</td><td>{label.manufactureDate}</td><td>{label.manufactureDate.slice(0, 7)}</td><td>{label.serialNumber.toString().padStart(6, "0")}</td><td className="row-actions"><button type="button" onClick={() => setStockDraft({ ...stockDraft, fromBarcode: label.barcodeValue })}>Set start</button><button type="button" onClick={() => setStockDraft({ ...stockDraft, toBarcode: label.barcodeValue })}>Set end</button></td></tr>)}</tbody></table>{pendingProduction.length === 0 && <p className="no-results">No pending labels match this department, month, or search.</p>}</div>
            </section></div>
          </>
        ) : active === "Label" ? (
          <>
            <div className="content-heading"><div><p className="eyebrow">CONTROL DESK / LABEL</p><h1>Label</h1></div><span className="date">06 OCT 2026</span></div>
            <div className="workspace">
              <div className="workspace-tabs">
                <button className={labelTab === "print" ? "selected" : ""} onClick={() => setLabelTab("print")}>Print label</button>
                {currentUserRole !== "QC" && (
                  <button className={labelTab === "duplicate" ? "selected" : ""} onClick={() => setLabelTab("duplicate")}>Print duplicate label</button>
                )}
              </div>
              {labelTab === "print" ? (
                <section className="master-panel label-panel">
                  <form className="label-form" onSubmit={generateLabels}>
                    <div><p className="form-kicker">LABEL RECORD</p><h2>Print label</h2></div>
                    <label className="wide-field" style={{ gridColumn: "span 2" }}>
                      Label Template
                      <select
                        value={labelDraft.templateId}
                        onChange={(event) => setLabelDraft({ ...labelDraft, templateId: event.target.value })}
                        required
                      >
                        <option value="">-- Select Label Template --</option>
                        {labelTemplates.map((template) => (
                          <option key={template.id} value={template.id}>
                            {template.name} ({template.widthMm} × {template.heightMm} mm){template.isDefault ? " [Default]" : ""}
                          </option>
                        ))}
                      </select>
                    </label>
                    <label>Department<select value={labelDraft.departmentId} onChange={(event) => { setLabelDraft({ ...labelDraft, departmentId: event.target.value, itemId: "" }); setLabelItemPicker(""); setIsItemPickerOpen(false); }}><option value="">All departments</option>{departments.map((department) => <option key={department.id} value={department.id}>{department.name}</option>)}</select></label>
                    <label className="wide-field">Item<div className="item-picker"><input value={labelItemPicker} placeholder="Type item ID or name" role="combobox" aria-expanded={isItemPickerOpen} aria-controls="label-item-options" onFocus={() => setIsItemPickerOpen(true)} onBlur={() => window.setTimeout(() => setIsItemPickerOpen(false), 120)} onChange={(event) => { setLabelItemPicker(event.target.value); setLabelDraft({ ...labelDraft, itemId: "" }); setIsItemPickerOpen(true); }} />{isItemPickerOpen && <div className="item-picker-options" id="label-item-options" role="listbox">{labelPickerItems.length > 0 ? labelPickerItems.map((item) => <button key={item.id} type="button" role="option" aria-selected={String(item.id) === labelDraft.itemId} onMouseDown={(event) => event.preventDefault()} onClick={() => { setLabelDraft({ ...labelDraft, itemId: String(item.id), departmentId: String(item.departmentId) }); setLabelItemPicker(`${item.code} - ${item.name}`); setIsItemPickerOpen(false); }}><strong>{item.code}</strong><span>{item.name}</span></button>) : <p>No matching items.</p>}</div>}</div></label>
                    <label>MFG Date<input type="date" value={labelDraft.manufactureDate} onChange={(event) => setLabelDraft({ ...labelDraft, manufactureDate: event.target.value })} /></label>
                    <label>Qty<input type="number" min="1" max="500" step="1" value={labelDraft.quantity} onChange={(event) => setLabelDraft({ ...labelDraft, quantity: event.target.value })} /></label>
                    {(() => {
                      const chosenTpl = labelTemplates.find((t) => String(t.id) === labelDraft.templateId);
                      if (!chosenTpl) return null;
                      const logoEl = chosenTpl.elements.find((e) => e.elementType === 1);
                      const is32Up = chosenTpl.widthMm <= 55 && chosenTpl.heightMm <= 35;
                      const logoDesc = logoEl ? (logoEl.logoName || logoEl.content || "Custom Logo") : "None";
                      return (
                        <div style={{ gridColumn: "1 / -1", background: "#f0fdf4", border: "1px solid #bbf7d0", padding: "10px 14px", borderRadius: "8px", display: "flex", justifyContent: "space-between", alignItems: "center", flexWrap: "wrap", gap: "8px" }}>
                          <div>
                            <span style={{ fontSize: "11px", color: "#166534", textTransform: "uppercase", fontWeight: 700, display: "block" }}>Active Label Layout</span>
                            <strong style={{ fontSize: "14px", color: "#14532d" }}>{chosenTpl.name}</strong>
                            <span style={{ fontSize: "12px", color: "#15803d", marginLeft: "8px" }}>({chosenTpl.widthMm} × {chosenTpl.heightMm} mm)</span>
                          </div>
                          <div style={{ display: "flex", gap: "12px", alignItems: "center", fontSize: "12px" }}>
                            <span style={{ color: "#166534" }}>
                              Template Logo: <strong>{logoDesc}</strong>
                            </span>
                            <span style={{ background: "#dcfce7", color: "#15803d", padding: "3px 8px", borderRadius: "4px", fontWeight: 600 }}>
                              {is32Up ? "⭐ Dynamic A4 Layout: 32 labels/page (4 cols × 8 rows)" : `Layout: ${chosenTpl.widthMm}×${chosenTpl.heightMm} mm`}
                            </span>
                          </div>
                        </div>
                      );
                    })()}
                    <div className="label-range"><span>Label Print From</span><strong>{labelRange.from || "000000"}</strong><span>To</span><strong>{labelRange.to || "000000"}</strong></div>
                    <button type="submit" className="primary label-generate">Generate <span>-&gt;</span></button>
                  </form>

                  <section className="label-item-list">
                    <div className="table-toolbar"><div><h2>Available Item Master records</h2><p>{labelItems.length} of {departmentLabelItems.length} item{departmentLabelItems.length === 1 ? "" : "s"} available for label generation</p></div><input value={labelItemSearch} onChange={(event) => setLabelItemSearch(event.target.value)} placeholder="Search item ID or name" aria-label="Search available items" /></div>
                    <div className="table-wrap"><table><thead><tr><th>Sr. No.</th><th>Item ID</th><th>Item Name</th><th>Description</th><th>Department</th><th>Packing</th><th>Gross Weight</th><th className="actions-heading">Select</th></tr></thead><tbody>{labelItems.map((item, index) => <tr key={item.id} className={String(item.id) === labelDraft.itemId ? "selected-row" : ""}><td>{index + 1}</td><td className="code-cell">{item.code}</td><td className="strong-cell">{item.name}</td><td>{item.description}</td><td>{departmentName(item.departmentId)}</td><td>{item.packing}</td><td>{item.weight.toFixed(2)}</td><td className="row-actions"><button type="button" onClick={() => { setLabelDraft({ ...labelDraft, itemId: String(item.id), departmentId: String(item.departmentId) }); setLabelItemPicker(`${item.code} - ${item.name}`); }}>Select</button></td></tr>)}</tbody></table>{labelItems.length === 0 && <p className="no-results">No items match this search.</p>}</div>
                  </section>
                </section>
              ) : (
                <section className="master-panel duplicate-label-panel">
                  <div className="coming inline-coming"><span>ADMIN</span><h2>Print duplicate label is queued next</h2><p>This tab is reserved for duplicate barcode workflows and will stay separate from normal label generation.</p></div>
                </section>
              )}
            </div>
          </>
        ) : active === "Reports" ? (
          <>
            <div className="content-heading"><div><p className="eyebrow">CONTROL DESK / REPORTS</p><h1>Reports</h1></div><span className="date">07 OCT 2026</span></div>
            <div className="workspace reports-workspace">
              <div className="workspace-tabs report-tabs">
                <button className={reportTab === "batchNo" ? "selected" : ""} onClick={() => setReportTab("batchNo")}>Batch No. report</button>
                <button className={reportTab === "customerSoDetails" ? "selected" : ""} onClick={() => setReportTab("customerSoDetails")}>Customer SO details</button>
                <button className={reportTab === "stock" ? "selected" : ""} onClick={() => setReportTab("stock")}>Stock report</button>
                <button className={reportTab === "salesOrder" ? "selected" : ""} onClick={() => setReportTab("salesOrder")}>Sales order report</button>
                <button className={reportTab === "itemDetail" ? "selected" : ""} onClick={() => setReportTab("itemDetail")}>Item detail report</button>
                <button className={reportTab === "productWiseSummary" ? "selected" : ""} onClick={() => setReportTab("productWiseSummary")}>Product-wise summary</button>
                <button className={reportTab === "batchWiseSummary" ? "selected" : ""} onClick={() => setReportTab("batchWiseSummary")}>Batch-wise summary</button>
                <button className={reportTab === "stockSummary" ? "selected" : ""} onClick={() => setReportTab("stockSummary")}>Stock summary</button>
                <button className={reportTab === "boxLabel" ? "selected" : ""} onClick={() => setReportTab("boxLabel")}>Box label report</button>
                <button className={reportTab === "salesOrderSummary" ? "selected" : ""} onClick={() => setReportTab("salesOrderSummary")}>Sales order summary</button>
                <button className={reportTab === "todaysPrintDetails" ? "selected" : ""} onClick={() => setReportTab("todaysPrintDetails")}>Today's print details</button>
              </div>
              <section className="master-panel report-panel">
                {reportTab === "todaysPrintDetails" && <><div className="report-heading"><p className="form-kicker">DAILY REPORT</p><h2>Daily print details</h2></div><div className="report-filters"><label>Manufacturing date<input type="date" value={reportDate} onChange={(event) => setReportDate(event.target.value)} /></label><button className="primary" onClick={loadDailyPrintReport}>Load <span>-&gt;</span></button></div><div className="table-wrap"><table><thead><tr><th>Sr. No.</th><th>Total print</th><th>Starting barcode</th><th>Ending barcode</th><th>Item ID</th><th>Department</th><th>Item</th><th>MFG date</th></tr></thead><tbody>{dailyPrintRows.map((row, index) => <tr key={`${row.itemCode}-${row.startingBarcode}`}><td>{index + 1}</td><td>{row.totalPrint}</td><td className="code-cell">{row.startingBarcode}</td><td className="code-cell">{row.endingBarcode}</td><td>{row.itemCode}</td><td>{row.departmentName}</td><td className="strong-cell">{row.itemName}</td><td>{row.manufactureDate}</td></tr>)}</tbody></table>{dailyPrintRows.length === 0 && <p className="no-results">Choose a date and load labels that have not been dispatched.</p>}</div></>}
                {(reportTab === "stockSummary" || reportTab === "stock") && <><div className="report-heading"><p className="form-kicker">STOCK REPORT</p><h2>{reportTab === "stock" ? "Current stock report" : "Stock report summary"}</h2></div><div className="report-filters"><label>Department<select value={reportDepartmentId} onChange={(event) => { setReportDepartmentId(event.target.value); setReportItemId(""); }}><option value="">All departments</option>{departments.map((department) => <option key={department.id} value={department.id}>{department.name}</option>)}</select></label><label>Item<select value={reportItemId} onChange={(event) => setReportItemId(event.target.value)}><option value="">All items</option>{items.filter((item) => !reportDepartmentId || String(item.departmentId) === reportDepartmentId).map((item) => <option key={item.id} value={item.id}>{item.code} - {item.name}</option>)}</select></label><button className="primary" onClick={loadStockReport}>Load <span>-&gt;</span></button>{reportTab === "stock" && <button className="secondary" onClick={() => openReportPrint({ title: "Stock Report", subtitle: reportDepartmentId ? departments.find((department) => String(department.id) === reportDepartmentId)?.name : "All departments", headers: ["Sr. No.", "Item", "Department", "Qty"], rows: stockReportRows.map((row, index) => [String(index + 1), row.itemName, row.departmentName, String(row.totalStockQuantity)]) })}>Print preview</button>}</div><div className="table-wrap"><table><thead><tr><th>Sr. No.</th><th>Item ID</th><th>Item name</th><th>Department</th><th>Total stock qty</th></tr></thead><tbody>{stockReportRows.map((row, index) => <tr key={row.itemCode}><td>{index + 1}</td><td className="code-cell">{row.itemCode}</td><td className="strong-cell">{row.itemName}</td><td>{row.departmentName}</td><td>{row.totalStockQuantity}</td></tr>)}</tbody></table>{stockReportRows.length === 0 && <p className="no-results">Load current available stock from production.</p>}</div></>}
                {reportTab === "itemDetail" && <><div className="report-heading"><p className="form-kicker">LABEL TRACE</p><h2>Item detail report</h2></div><div className="report-filters"><label>Barcode or serial ending<input value={reportBarcode} onChange={(event) => setReportBarcode(event.target.value.replace(/\D/g, ""))} placeholder="Full barcode or ending digits" /></label><button className="primary" onClick={loadItemDetailReport}>Search <span>-&gt;</span></button></div>{itemDetailReport && <dl className="report-detail"><dt>Barcode</dt><dd className="code-cell">{itemDetailReport.barcodeValue}</dd><dt>Item</dt><dd>{itemDetailReport.itemName} ({itemDetailReport.itemCode})</dd><dt>Department</dt><dd>{itemDetailReport.departmentName}</dd><dt>Batch No.</dt><dd>{itemDetailReport.batchNumber ?? "Not in stock"}</dd><dt>Stock status</dt><dd>{itemDetailReport.status}</dd><dt>Customer</dt><dd>{itemDetailReport.customerName ?? "Not dispatched"}</dd><dt>Sales order</dt><dd>{itemDetailReport.salesOrderNumber ?? "-"}</dd></dl>}</>}
                {reportTab === "batchNo" && <><div className="report-heading"><p className="form-kicker">BATCH TRACE</p><h2>Batch No. report</h2></div><div className="report-filters"><label>Batch No.<input value={reportBatch} onChange={(event) => setReportBatch(event.target.value)} placeholder="Enter batch number" /></label><button className="primary" onClick={loadBatchReport}>Load <span>-&gt;</span></button><button className="secondary" onClick={() => openReportPrint({ title: "Report as per Batch No.", subtitle: reportBatch, headers: ["Batch No.", "Box Code", "Item", "Sales Order No.", "Dispatch Date", "Customer Name"], rows: batchReportRows.map((row) => [row.batchNumber, row.barcodeValue, row.itemName, row.salesOrderNumber ?? "-", row.dispatchDate ?? "-", row.customerName ?? "-"]) })}>Print preview</button></div><div className="table-wrap"><table><thead><tr><th>Batch No.</th><th>Box code</th><th>Item</th><th>Sales order No.</th><th>Dispatch date</th><th>Customer name</th></tr></thead><tbody>{batchReportRows.map((row) => <tr key={row.barcodeValue}><td>{row.batchNumber}</td><td className="code-cell">{row.barcodeValue}</td><td>{row.itemName}</td><td>{row.salesOrderNumber ?? "-"}</td><td>{row.dispatchDate ?? "-"}</td><td>{row.customerName ?? "-"}</td></tr>)}</tbody></table>{batchReportRows.length === 0 && <p className="no-results">Enter a batch number to trace its labels.</p>}</div></>}
                {reportTab === "customerSoDetails" && <><div className="report-heading"><p className="form-kicker">CUSTOMER REPORT</p><h2>Customer sales-order details</h2></div><div className="report-filters"><label>Customer<select value={reportCustomerId} onChange={(event) => setReportCustomerId(event.target.value)}><option value="">Select customer</option>{customers.map((customer) => <option key={customer.id} value={customer.id}>{customer.name}</option>)}</select></label><button className="primary" onClick={loadCustomerReport}>Load <span>-&gt;</span></button>{customerReport && <button className="secondary" onClick={() => openReportPrint({ title: "Customer Sales Order Details", address: [customerReport.customer.name, ...packingAddressLines(customerReport.customer)], headers: ["SO No.", "Invoice No.", "Dispatch Date", "Batch No.", "Item", "Qty"], rows: customerReport.rows.map((row) => [row.salesOrderNumber, row.invoiceNumber, row.dispatchDate, row.batchNumber, row.itemName, String(row.quantity)]) })}>Print preview</button>}</div>{customerReport && <><div className="report-customer-address"><strong>{customerReport.customer.name}</strong>{packingAddressLines(customerReport.customer).map((line) => <span key={line}>{line}</span>)}</div><div className="table-wrap"><table><thead><tr><th>SO No.</th><th>Invoice No.</th><th>Dispatch date</th><th>Batch No.</th><th>Item</th><th>Qty</th></tr></thead><tbody>{customerReport.rows.map((row, index) => <tr key={`${row.salesOrderNumber}-${row.batchNumber}-${index}`}><td>{row.salesOrderNumber}</td><td>{row.invoiceNumber}</td><td>{row.dispatchDate}</td><td>{row.batchNumber}</td><td>{row.itemName}</td><td>{row.quantity}</td></tr>)}</tbody></table></div></>}</>}
                {reportTab === "salesOrderSummary" && <><div className="report-heading"><p className="form-kicker">SALES ORDER</p><h2>Sales order summary report</h2></div><div className="report-filters"><label>Sales Order No.<input value={reportSalesOrder} onChange={(event) => setReportSalesOrder(event.target.value)} placeholder="Enter sales order number" /></label><button className="primary" onClick={loadSalesOrderSummary}>Load <span>-&gt;</span></button><button className="secondary" onClick={() => openReportPrint({ title: "Sales Order Summary Report", subtitle: `Sales Order: ${reportSalesOrder}`, headers: ["SO No.", "Item", "Qty"], rows: salesOrderSummaryRows.map((row) => [row.salesOrderNumber, row.itemName, String(row.quantity)]) })}>Print preview</button></div><div className="table-wrap"><table><thead><tr><th>SO No.</th><th>Item</th><th>Qty</th></tr></thead><tbody>{salesOrderSummaryRows.map((row, index) => <tr key={`${row.salesOrderNumber}-${row.itemName}-${index}`}><td>{row.salesOrderNumber}</td><td>{row.itemName}</td><td>{row.quantity}</td></tr>)}</tbody></table>{salesOrderSummaryRows.length > 0 && <p className="report-total">Total Qty: {salesOrderSummaryRows.reduce((sum, row) => sum + row.quantity, 0)}</p>}</div></>}
                {(reportTab === "batchWiseSummary" || reportTab === "productWiseSummary") && <><div className="report-heading"><p className="form-kicker">{reportTab === "batchWiseSummary" ? "BATCH SUMMARY" : "PRODUCT SUMMARY"}</p><h2>{reportTab === "batchWiseSummary" ? "Batch-wise report summary" : "Product-wise report summary"}</h2></div><div className="report-filters">{reportTab === "batchWiseSummary" ? <label>Batch<div className="item-picker"><input value={reportBatch} placeholder="All batches - type to search" role="combobox" aria-expanded={isReportBatchOpen} onFocus={() => setIsReportBatchOpen(true)} onBlur={() => window.setTimeout(() => setIsReportBatchOpen(false), 120)} onChange={(event) => { setReportBatch(event.target.value); setIsReportBatchOpen(true); }} />{isReportBatchOpen && <div className="item-picker-options" role="listbox">{(() => { const known = Array.from(new Set([...batchOptions, ...batchReportRows.map((row) => row.batchNumber), ...inventorySummaryRows.map((row) => row.batchNumber), ...boxDetailRows.map((row) => row.batchNumber)])); const query = reportBatch.trim().toLowerCase(); const filtered = known.filter((batch) => !query || batch.toLowerCase().includes(query)); return (<>{filtered.map((batch) => <button key={batch} type="button" role="option" onMouseDown={(event) => event.preventDefault()} onClick={() => { setReportBatch(batch); setIsReportBatchOpen(false); }}><strong>{batch}</strong><span>Batch {batch}</span></button>)}{filtered.length === 0 && <p>{known.length === 0 ? "No batches yet - add stock first." : "No matching batches."}</p>}</>); })()}</div>}</div></label> : <label>Item<div className="item-picker"><input value={reportItemSearch || (reportItemId ? `${items.find((item) => String(item.id) === reportItemId)?.code ?? ""} - ${items.find((item) => String(item.id) === reportItemId)?.name ?? ""}` : "")} placeholder="All items - type code or name to search" role="combobox" aria-expanded={isReportItemOpen} onFocus={() => setIsReportItemOpen(true)} onBlur={() => window.setTimeout(() => setIsReportItemOpen(false), 120)} onChange={(event) => { setReportItemSearch(event.target.value); setReportItemId(""); setIsReportItemOpen(true); }} />{isReportItemOpen && <div className="item-picker-options" role="listbox">{(() => { const query = reportItemSearch.trim().toLowerCase(); const filtered = items.filter((item) => !query || `${item.code} ${item.name}`.toLowerCase().includes(query)); return (<><button type="button" role="option" onMouseDown={(event) => event.preventDefault()} onClick={() => { setReportItemId(""); setReportItemSearch(""); setIsReportItemOpen(false); }}><strong>ALL</strong><span>All items</span></button>{filtered.map((item) => <button key={item.id} type="button" role="option" aria-selected={String(item.id) === reportItemId} onMouseDown={(event) => event.preventDefault()} onClick={() => { setReportItemId(String(item.id)); setReportItemSearch(`${item.code} - ${item.name}`); setIsReportItemOpen(false); }}><strong>{item.code}</strong><span>{item.name}</span></button>)}{filtered.length === 0 && <p>No matching items.</p>}</>); })()}</div>}</div></label>}<button className="primary" onClick={() => loadInventorySummary(reportTab === "batchWiseSummary" ? "batch-wise" : "product-wise")}>Load <span>-&gt;</span></button><button className="secondary" onClick={() => exportReport(inventorySummaryRows.map((row, index) => [String(index + 1), row.itemName, row.departmentName, row.customerName, row.batchNumber, String(row.totalBoxQuantity)]), ["Sr. No.", "Item Name", "Department", "Customer", "Batch", "Total Box Qty"], reportTab === "batchWiseSummary" ? "batch-wise-report" : "product-wise-report")}>Export CSV</button></div><div className="table-wrap"><table><thead><tr><th>Sr. No.</th><th>Item name</th><th>Department</th><th>Customer</th><th>Batch</th><th>Total box qty</th></tr></thead><tbody>{inventorySummaryRows.map((row, index) => <tr key={`${row.itemName}-${row.batchNumber}-${row.customerName}-${index}`}><td>{index + 1}</td><td>{row.itemName}</td><td>{row.departmentName}</td><td>{row.customerName}</td><td>{row.batchNumber}</td><td>{row.totalBoxQuantity}</td></tr>)}</tbody></table></div></>}
                {reportTab === "salesOrder" && <><div className="report-heading"><p className="form-kicker">SALES ORDER</p><h2>Sales order report</h2></div><div className="report-filters"><label>Sales Order No.<input value={reportSalesOrder} onChange={(event) => setReportSalesOrder(event.target.value)} placeholder="Search saved sales order" /></label><button className="primary" onClick={loadSalesOrderSummary}>Load <span>-&gt;</span></button><button className="secondary" onClick={() => openReportPrint({ title: "Sales Order Report", subtitle: `Sales Order: ${reportSalesOrder}`, headers: ["SO No.", "Item", "Qty"], rows: salesOrderSummaryRows.map((row) => [row.salesOrderNumber, row.itemName, String(row.quantity)]) })}>Print preview</button></div><div className="table-wrap"><table><thead><tr><th>SO No.</th><th>Item</th><th>Qty</th></tr></thead><tbody>{salesOrderSummaryRows.map((row, index) => <tr key={`${row.salesOrderNumber}-${row.itemName}-${index}`}><td>{row.salesOrderNumber}</td><td>{row.itemName}</td><td>{row.quantity}</td></tr>)}</tbody></table></div></>}
                {reportTab === "boxLabel" && <><div className="report-heading"><p className="form-kicker">BOX TRACE</p><h2>Box detail summary report</h2></div><div className="report-filters"><label>Department<select value={reportDepartmentId} onChange={(event) => setReportDepartmentId(event.target.value)}><option value="">All departments</option>{departments.map((department) => <option key={department.id} value={department.id}>{department.name}</option>)}</select></label><label>Item<select value={reportItemId} onChange={(event) => setReportItemId(event.target.value)}><option value="">All items</option>{items.filter((item) => !reportDepartmentId || String(item.departmentId) === reportDepartmentId).map((item) => <option key={item.id} value={item.id}>{item.code} - {item.name}</option>)}</select></label><label>Start box<input value={reportFromBarcode} onChange={(event) => setReportFromBarcode(event.target.value.replace(/\D/g, ""))} /></label><label>End box<input value={reportToBarcode} onChange={(event) => setReportToBarcode(event.target.value.replace(/\D/g, ""))} /></label><button className="primary" onClick={loadBoxDetails}>Load <span>-&gt;</span></button><button className="secondary" onClick={() => exportReport(boxDetailRows.map((row, index) => [String(index + 1), row.itemName, row.batchNumber, row.barcodeValue, row.stockStatus, row.salesOrderNumber ?? "", row.customerName]), ["Sr. No.", "Item Name", "Batch", "Box Barcode", "Stock Status", "Sales Order", "Customer"], "box-detail-report")}>Export CSV</button></div><div className="table-wrap"><table><thead><tr><th>Sr. No.</th><th>Item name</th><th>Batch</th><th>Box barcode</th><th>Stock status</th><th>Sales order</th><th>Customer</th></tr></thead><tbody>{boxDetailRows.map((row, index) => <tr key={row.barcodeValue}><td>{index + 1}</td><td>{row.itemName}</td><td>{row.batchNumber}</td><td className="code-cell">{row.barcodeValue}</td><td>{row.stockStatus}</td><td>{row.salesOrderNumber ?? "-"}</td><td>{row.customerName}</td></tr>)}</tbody></table></div></>}
              </section>
            </div>
          </>
        ) : active === "More" ? (
          <>
            <div className="content-heading">
              <div>
                <p className="eyebrow">ADMINISTRATION / MORE</p>
                <h1>System &amp; device configuration</h1>
              </div>
              <span className="date">SERVER PC</span>
            </div>
            <div className="workspace">
              <div className="workspace-tabs">
                <button
                  className={moreTab === "printing" ? "selected" : ""}
                  onClick={() => setMoreTab("printing")}
                >
                  Printing
                </button>
              </div>

              {moreTab === "printing" && (
                <div className="master-panel">
                  <div className="table-toolbar" style={{ paddingTop: 0, paddingBottom: "20px" }}>
                    <div>
                      <h2>Server printer subsystem</h2>
                      <p>
                        Printers installed on the Admin/Server PC. Configure independent devices for documents and barcodes.
                      </p>
                    </div>
                    <div className="toolbar-actions">
                      <button
                        className="secondary"
                        onClick={loadPrintersAndConfig}
                        disabled={loadingPrinters}
                      >
                        {loadingPrinters ? "Scanning..." : "Rescan PC printers"}
                      </button>
                    </div>
                  </div>

                  <div className="workspace-tabs" style={{ marginBottom: "24px", padding: 0 }}>
                    <button
                      className={printingSubTab === "regular" ? "selected" : ""}
                      onClick={() => setPrintingSubTab("regular")}
                    >
                      A. Regular Document Print
                    </button>
                    <button
                      className={printingSubTab === "barcode" ? "selected" : ""}
                      onClick={() => setPrintingSubTab("barcode")}
                    >
                      B. Barcode Print
                    </button>
                  </div>

                  {printingSubTab === "regular" && (
                    <article className="printer-card">
                      <p className="form-kicker">CATEGORY 1 — NORMAL DOCUMENTS</p>
                      <h3>Regular Document Printing</h3>
                      <p className="description">
                        Dedicated printer for PDF documents, stock reports, packing sheets, and daily registers generated by BoxTrack.
                      </p>

                      <div className="printer-current-box">
                        <div>
                          <span style={{ fontSize: "11px", color: "#697771", display: "block", marginBottom: "4px" }}>
                            CURRENT SELECTED PRINTER
                          </span>
                          <span className="printer-current-name">
                            {printerConfig.regularDocumentPrinter?.printerName ?? "Not configured"}
                          </span>
                        </div>
                        <div>
                          {printerConfig.regularDocumentPrinter ? (
                            <>
                              <span className="printer-badge badge-ready">
                                {availablePrinters.find((p) => p.name === printerConfig.regularDocumentPrinter?.printerName)?.status ?? "Ready"}
                              </span>
                              {availablePrinters.find((p) => p.name === printerConfig.regularDocumentPrinter?.printerName)?.isDefault && (
                                <span className="printer-badge badge-default">OS Default</span>
                              )}
                            </>
                          ) : (
                            <span className="printer-badge badge-unconfigured">Not configured</span>
                          )}
                        </div>
                      </div>

                      <div className="master-form" style={{ gridTemplateColumns: "1fr", borderBottom: 0, paddingBottom: 0 }}>
                        <label>
                          Available printers on Server PC
                          <select
                            value={selectedRegularPrinter}
                            onChange={(e) => setSelectedRegularPrinter(e.target.value)}
                          >
                            <option value="">-- Select a printer --</option>
                            {availablePrinters.map((p) => (
                              <option key={p.name} value={p.name}>
                                {p.displayName} ({p.status}){p.isDefault ? " [Default]" : ""}
                              </option>
                            ))}
                          </select>
                        </label>

                        <div className="printer-card-actions" style={{ marginTop: "12px" }}>
                          <button
                            type="button"
                            className="primary"
                            disabled={!selectedRegularPrinter || savingPrinter || selectedRegularPrinter === printerConfig.regularDocumentPrinter?.printerName}
                            onClick={() => savePrinterConfig("regular")}
                          >
                            {savingPrinter ? "Saving..." : "Save Regular Printer"}
                          </button>
                          <button
                            type="button"
                            className="secondary"
                            disabled={!printerConfig.regularDocumentPrinter || testingPrinter}
                            onClick={() => runTestPrint("RegularDocument")}
                          >
                            {testingPrinter ? "Sending print..." : "Test Print (Document)"}
                          </button>
                        </div>
                      </div>
                    </article>
                  )}

                  {printingSubTab === "barcode" && (
                    <article className="printer-card">
                      <p className="form-kicker">CATEGORY 2 — BARCODE LABELS</p>
                      <h3>Barcode Printing</h3>
                      <p className="description">
                        Dedicated barcode label printing subsystem supporting both Laser and TSC thermal printer architectures.
                      </p>

                      {/* Barcode Mode Selector: Laser vs TSC */}
                      <div className="workspace-tabs" style={{ marginBottom: "16px" }}>
                        <button
                          type="button"
                          className={barcodeMode === "laser" ? "selected" : ""}
                          onClick={() => setBarcodeMode("laser")}
                        >
                          🖨️ Laser Printer Mode
                        </button>
                        <button
                          type="button"
                          className={barcodeMode === "tsc" ? "selected" : ""}
                          onClick={() => setBarcodeMode("tsc")}
                        >
                          🏷️ TSC Thermal Printer (TSPL/TSPL-EZ)
                        </button>
                      </div>

                      <div style={{ display: "flex", gap: "8px", marginBottom: "16px" }}>
                        <button
                          type="button"
                          className={tscSubTab === "settings" ? "primary" : "secondary"}
                          onClick={() => setTscSubTab("settings")}
                          style={{ fontSize: "12px", padding: "6px 12px" }}
                        >
                          {barcodeMode === "tsc" ? "TSC Connection & Config" : "Laser Connection & A4 Grid"}
                        </button>
                        <button
                          type="button"
                          className={tscSubTab === "editor" ? "primary" : "secondary"}
                          onClick={() => setTscSubTab("editor")}
                          style={{ fontSize: "12px", padding: "6px 12px" }}
                        >
                          🎨 Visual Label Template Editor
                        </button>
                      </div>

                      {tscSubTab === "editor" ? (
                        <TscLabelEditor
                          apiUrl={apiUrl}
                          apiToken={apiToken}
                          onNotify={notify}
                          activeBarcodePrinter={printerConfig.barcodePrinter?.printerName ?? null}
                          onRefreshJobs={loadRecentPrintJobs}
                          onTemplatesChanged={loadTemplates}
                        />
                      ) : (
                        <>
                          <div className="printer-current-box">
                            <div>
                              <span style={{ fontSize: "11px", color: "#697771", display: "block", marginBottom: "4px" }}>
                                CURRENT SELECTED {barcodeMode === "tsc" ? "TSC (203 DPI)" : "LASER"} PRINTER
                              </span>
                              <span className="printer-current-name">
                                {printerConfig.barcodePrinter?.printerName ?? "Not configured"}
                              </span>
                              {barcodeMode === "tsc" ? (
                                <span style={{ fontSize: "11px", color: "#008060", display: "block", marginTop: "2px" }}>
                                  Target: {printerConfig.barcodePrinter?.model || "TSC TTP-247"} (203 DPI, 1mm = 8 dots)
                                </span>
                              ) : (
                                <span style={{ fontSize: "11px", color: "#2563eb", display: "block", marginTop: "2px" }}>
                                  Multi-label A4 Sheet Compositor (tiled vector GDI+ output)
                                </span>
                              )}
                            </div>
                            <div>
                              {printerConfig.barcodePrinter ? (
                                <>
                                  <span className="printer-badge badge-ready">
                                    {availablePrinters.find((p) => p.name === printerConfig.barcodePrinter?.printerName)?.status ?? "Ready"}
                                  </span>
                                  {availablePrinters.find((p) => p.name === printerConfig.barcodePrinter?.printerName)?.isDefault && (
                                    <span className="printer-badge badge-default">OS Default</span>
                                  )}
                                </>
                              ) : (
                                <span className="printer-badge badge-unconfigured">Not configured</span>
                              )}
                            </div>
                          </div>

                          <div className="master-form" style={{ gridTemplateColumns: "1fr", borderBottom: 0, paddingBottom: 0 }}>
                            <label>
                              Available printers on Server PC
                              <select
                                value={selectedBarcodePrinter}
                                onChange={(e) => setSelectedBarcodePrinter(e.target.value)}
                              >
                                <option value="">-- Select a printer --</option>
                                {availablePrinters.map((p) => (
                                  <option key={p.name} value={p.name}>
                                    {p.displayName} ({p.status}){p.isDefault ? " [Default]" : ""}
                                  </option>
                                ))}
                              </select>
                            </label>

                            {barcodeMode === "laser" && (
                              <div className="laser-a4-settings-panel" style={{ marginTop: "16px", padding: "16px", background: "#f8fafc", border: "1px solid #cbd5e1", borderRadius: "8px" }}>
                                <h4 style={{ margin: "0 0 6px 0", fontSize: "14px", color: "#1e293b", fontWeight: 600 }}>📄 Laser A4 Sheet &amp; Multi-Label Grid Settings</h4>
                                <p style={{ margin: "0 0 16px 0", fontSize: "12px", color: "#64748b" }}>
                                  Labels are tiled across each A4 sheet according to these margins and gaps. It will NOT print 1 label per page.
                                </p>

                                <div style={{ marginBottom: "14px", background: "#ffffff", padding: "10px 14px", borderRadius: "6px", border: "1px solid #cbd5e1" }}>
                                  <label style={{ fontSize: "12px", fontWeight: 600, color: "#334155", display: "block", marginBottom: "6px" }}>
                                    Active Label Template for Laser Grid
                                  </label>
                                  <select
                                    value={selectedBarcodeTemplateId ?? ""}
                                    onChange={(e) => {
                                      const val = e.target.value ? Number(e.target.value) : null;
                                      setSelectedBarcodeTemplateId(val);
                                    }}
                                    style={{ width: "100%", padding: "7px 10px", fontSize: "13px", borderRadius: "6px", border: "1px solid #94a3b8" }}
                                  >
                                    <option value="">-- Auto / Use Default Template --</option>
                                    {labelTemplates.map((t) => (
                                      <option key={t.id} value={t.id}>
                                        {t.name} ({t.widthMm} × {t.heightMm} mm) {t.isDefault ? "★ Default" : ""}
                                      </option>
                                    ))}
                                  </select>
                                  <span style={{ fontSize: "11px", color: "#64748b", marginTop: "4px", display: "block" }}>
                                    Label size for calculation: <strong>{laserCalculations.labelW} × {laserCalculations.labelH} mm</strong> ({laserCalculations.templateName})
                                  </span>
                                </div>

                                <div style={{ display: "grid", gridTemplateColumns: "repeat(auto-fit, minmax(130px, 1fr))", gap: "12px", marginBottom: "16px" }}>
                                  <label style={{ fontSize: "12px" }}>
                                    Paper Size
                                    <select value={laserPaperSize} onChange={(e) => {
                                      setLaserPaperSize(e.target.value);
                                      if (e.target.value === "A4") { setLaserPaperWidthMm(210); setLaserPaperHeightMm(297); }
                                      else if (e.target.value === "Letter") { setLaserPaperWidthMm(216); setLaserPaperHeightMm(279); }
                                    }}>
                                      <option value="A4">A4 (210 × 297 mm)</option>
                                      <option value="Letter">Letter (216 × 279 mm)</option>
                                      <option value="Custom">Custom size</option>
                                    </select>
                                  </label>
                                  <label style={{ fontSize: "12px" }}>
                                    Paper Width (mm)
                                    <input type="number" step="0.5" value={laserPaperWidthMm} onChange={(e) => setLaserPaperWidthMm(Number(e.target.value))} />
                                  </label>
                                  <label style={{ fontSize: "12px" }}>
                                    Paper Height (mm)
                                    <input type="number" step="0.5" value={laserPaperHeightMm} onChange={(e) => setLaserPaperHeightMm(Number(e.target.value))} />
                                  </label>
                                  <label style={{ fontSize: "12px" }}>
                                    Margin Left (mm)
                                    <input type="number" step="0.5" value={laserMarginLeftMm} onChange={(e) => setLaserMarginLeftMm(Number(e.target.value))} />
                                  </label>
                                  <label style={{ fontSize: "12px" }}>
                                    Margin Right (mm)
                                    <input type="number" step="0.5" value={laserMarginRightMm} onChange={(e) => setLaserMarginRightMm(Number(e.target.value))} />
                                  </label>
                                  <label style={{ fontSize: "12px" }}>
                                    Margin Top (mm)
                                    <input type="number" step="0.5" value={laserMarginTopMm} onChange={(e) => setLaserMarginTopMm(Number(e.target.value))} />
                                  </label>
                                  <label style={{ fontSize: "12px" }}>
                                    Margin Bottom (mm)
                                    <input type="number" step="0.5" value={laserMarginBottomMm} onChange={(e) => setLaserMarginBottomMm(Number(e.target.value))} />
                                  </label>
                                  <label style={{ fontSize: "12px" }}>
                                    H-Gap (mm)
                                    <input type="number" step="0.5" value={laserGapXMm} onChange={(e) => setLaserGapXMm(Number(e.target.value))} />
                                  </label>
                                  <label style={{ fontSize: "12px" }}>
                                    V-Gap (mm)
                                    <input type="number" step="0.5" value={laserGapYMm} onChange={(e) => setLaserGapYMm(Number(e.target.value))} />
                                  </label>
                                </div>

                                <div style={{ background: "#ffffff", border: "1px solid #cbd5e1", borderRadius: "6px", padding: "12px", display: "grid", gridTemplateColumns: "repeat(auto-fit, minmax(130px, 1fr))", gap: "10px", alignItems: "center" }}>
                                  <div>
                                    <span style={{ fontSize: "10px", color: "#64748b", textTransform: "uppercase", display: "block" }}>Usable Area</span>
                                    <strong style={{ fontSize: "13px", color: "#0f172a" }}>{laserCalculations.usableW} × {laserCalculations.usableH} mm</strong>
                                  </div>
                                  <div>
                                    <span style={{ fontSize: "10px", color: "#64748b", textTransform: "uppercase", display: "block" }}>Grid Layout</span>
                                    <strong style={{ fontSize: "13px", color: "#0f172a" }}>{laserCalculations.cols} cols × {laserCalculations.rows} rows</strong>
                                  </div>
                                  <div>
                                    <span style={{ fontSize: "10px", color: "#64748b", textTransform: "uppercase", display: "block" }}>Capacity / Sheet</span>
                                    <strong style={{ fontSize: "14px", color: "#16a34a" }}>⭐ {laserCalculations.perPage} labels/page</strong>
                                  </div>
                                  <div>
                                    <span style={{ fontSize: "10px", color: "#64748b", textTransform: "uppercase", display: "block" }}>Label Size</span>
                                    <strong style={{ fontSize: "13px", color: "#0f172a" }}>{laserCalculations.labelW} × {laserCalculations.labelH} mm</strong>
                                  </div>
                                </div>
                              </div>
                            )}

                            <div className="printer-card-actions" style={{ marginTop: "16px" }}>
                              <button
                                type="button"
                                className="primary"
                                disabled={!selectedBarcodePrinter || savingPrinter}
                                onClick={() => savePrinterConfig("barcode")}
                              >
                                {savingPrinter ? "Saving..." : `Save ${barcodeMode === "tsc" ? "TSC" : "Laser"} Printer & Settings`}
                              </button>
                              <button
                                type="button"
                                className="secondary"
                                disabled={!printerConfig.barcodePrinter || testingPrinter}
                                onClick={() => runTestPrint("Barcode")}
                              >
                                {testingPrinter ? "Sending barcode..." : `Test Barcode (${barcodeMode === "tsc" ? "TSPL RAW" : "Laser A4 Grid"})`}
                              </button>
                            </div>
                          </div>
                        </>
                      )}
                    </article>
                  )}

                  <div style={{ marginTop: "32px" }}>
                    <div className="table-toolbar">
                      <div>
                        <h2>Server print jobs</h2>
                        <p>Track recent jobs dispatched to server-connected printers.</p>
                      </div>
                    </div>
                    <div className="table-wrap">
                      <table>
                        <thead>
                          <tr>
                            <th>Job #</th>
                            <th>Category</th>
                            <th>Printer name</th>
                            <th>Document</th>
                            <th>Status</th>
                            <th>Requested by</th>
                            <th>Timestamp</th>
                            <th>Details</th>
                          </tr>
                        </thead>
                        <tbody>
                          {recentPrintJobs.map((job) => (
                            <tr key={job.id}>
                              <td>{job.id}</td>
                              <td className="strong-cell">{job.category}</td>
                              <td>{job.printerName}</td>
                              <td>{job.documentName}</td>
                              <td>
                                <span className={`printer-badge ${job.status === "Completed" ? "badge-ready" : job.status === "Failed" ? "badge-offline" : "badge-default"}`}>
                                  {job.status}
                                </span>
                              </td>
                              <td>{job.requestedBy ?? "Admin"}</td>
                              <td>{new Date(job.createdAt).toLocaleTimeString()}</td>
                              <td>{job.errorMessage ?? (job.documentReference ? "PDF saved" : "OK")}</td>
                            </tr>
                          ))}
                        </tbody>
                      </table>
                      {recentPrintJobs.length === 0 && (
                        <p className="no-results">No print jobs executed yet. Use "Test Print" to test printer communication.</p>
                      )}
                    </div>
                  </div>
                </div>
              )}
            </div>
          </>
        ) : active === "User" ? (
          <>
            <div className="content-heading">
              <div>
                <p className="eyebrow">ADMINISTRATION / ACCESS CONTROL</p>
                <h1>User accounts &amp; authentication</h1>
              </div>
              <span className="date">ACCESS DESK</span>
            </div>
            <div className="workspace">
              <div className="master-panel">
                <div className="table-toolbar" style={{ paddingTop: 0, paddingBottom: "20px" }}>
                  <div>
                    <h2>Configured user accounts</h2>
                    <p>System users configured for Admin, QC, and Department-scoped Production access.</p>
                  </div>
                  <div className="toolbar-actions">
                    <button className="secondary" onClick={loadUserAccounts} disabled={loadingUsers}>
                      {loadingUsers ? "Refreshing..." : "Refresh users"}
                    </button>
                  </div>
                </div>

                <div style={{ background: "#fff7ed", border: "1px solid #fdba74", padding: "12px 16px", borderRadius: "6px", marginBottom: "20px" }}>
                  <strong style={{ color: "#c2410c", display: "block", marginBottom: "4px" }}>
                    DEVELOPMENT ONLY — TEMPORARY PASSWORDS DISPLAYED (MUST BE REMOVED BEFORE PRODUCTION)
                  </strong>
                  <span style={{ fontSize: "13px", color: "#9a3412" }}>
                    The passwords below are development/test defaults. They are visible only to logged-in Administrators on this PC and MUST BE REMOVED before production deployment.
                  </span>
                </div>

                <div className="table-wrap">
                  <table>
                    <thead>
                      <tr>
                        <th>Username</th>
                        <th>Role</th>
                        <th>Assigned Department</th>
                        <th>Temporary Dev Password</th>
                        <th>Status</th>
                        <th>Created</th>
                      </tr>
                    </thead>
                    <tbody>
                      {userAccounts.map((u) => (
                        <tr key={u.id}>
                          <td className="strong-cell"><code>{u.username}</code></td>
                          <td>
                            <span className={`printer-badge ${u.role === "Admin" ? "badge-ready" : u.role === "QC" ? "badge-default" : "badge-ready"}`}>
                              {u.role}
                            </span>
                          </td>
                          <td>{u.departmentName ? <strong>{u.departmentName}</strong> : <span style={{ color: "#888" }}>All / Global</span>}</td>
                          <td>
                            <code style={{ background: "#fef3c7", padding: "2px 8px", borderRadius: "4px", color: "#92400e", fontWeight: 600 }}>
                              {u.temporaryDevPassword ?? "—"}
                            </code>
                          </td>
                          <td>
                            <span className="printer-badge badge-ready">{u.isActive ? "Active" : "Inactive"}</span>
                          </td>
                          <td>{new Date(u.createdAt).toLocaleDateString()}</td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                  {userAccounts.length === 0 && !loadingUsers && (
                    <p className="no-results">No user accounts found.</p>
                  )}
                </div>
              </div>
            </div>
          </>
        ) : (
          <div className="coming">
            <span>SOON</span>
            <h2>{active} workflows are queued for Phase 2+</h2>
            <p>
              This menu stays visible so the operating model remains familiar.
            </p>
          </div>
        )}
      </section>
      {message && (
        <div className="toast" role="status">
          {message}
        </div>
      )}
      {reportPrintDocument && <div className="modal-backdrop packing-backdrop" role="presentation" onMouseDown={() => setReportPrintDocument(null)}><section className="packing-dialog report-print-dialog" role="dialog" aria-modal="true" onMouseDown={(event) => event.stopPropagation()}><div className="packing-dialog-actions no-print"><div><p className="form-kicker">REPORT DOCUMENT</p><h2>{reportPrintDocument.title}</h2></div><div><button type="button" className="secondary" onClick={downloadReportPdf}>Download PDF</button><button type="button" className="primary" onClick={printReportDocument}>Print <span>-&gt;</span></button><button type="button" className="dialog-close" onClick={() => setReportPrintDocument(null)} aria-label="Close report preview">X</button></div></div><article className="packing-sheet report-print-sheet"><time>{new Date().toLocaleDateString("en-GB", { day: "2-digit", month: "short", year: "2-digit" })}</time><h1>{reportPrintDocument.title}</h1>{reportPrintDocument.subtitle && <p>{reportPrintDocument.subtitle}</p>}{reportPrintDocument.address && <section className="packing-ship-to">{reportPrintDocument.address.map((line, index) => index === 0 ? <b key={line}>{line}</b> : <span key={line}>{line}</span>)}</section>}<table className="packing-table report-print-table"><thead><tr>{reportPrintDocument.headers.map((header) => <th key={header}>{header}</th>)}</tr></thead><tbody>{reportPrintDocument.rows.map((row, index) => <tr key={index}>{row.map((cell, cellIndex) => <td key={`${index}-${cellIndex}`}>{cell}</td>)}</tr>)}</tbody></table></article></section></div>}
      {(importIssues.length > 0 || duplicateComparisons.length > 0 || customerDuplicateComparisons.length > 0) && (
        <div className="modal-backdrop" role="presentation">
          <section
            className="import-dialog"
            role="dialog"
            aria-modal="true"
            aria-labelledby="import-result-title"
          >
            <div className="dialog-heading">
              <div>
                <p className="form-kicker">IMPORT REVIEW</p>
                <h2 id="import-result-title">Some rows were not imported</h2>
              </div>
              <button
                className="dialog-close"
                onClick={closeImportReview}
                aria-label="Close import review"
              >
                X
              </button>
            </div>
            <p className="dialog-summary">
              The valid rows were added. Existing IDs were not inserted again. Compare the database record with the uploaded row, then explicitly update the existing record if needed.
            </p>
            {duplicateComparisons.length > 0 && <div className="duplicate-comparisons">
              {duplicateComparisons.map((comparison) => <article className="duplicate-card" key={`${comparison.row}-${comparison.existing.code}`}>
                <div className="duplicate-card-heading"><strong>Row {comparison.row} / Item {comparison.existing.code}</strong><span>Existing database record vs uploaded record</span></div>
                <div className="comparison-grid">
                  <div><p className="comparison-label">CURRENT DATABASE</p><dl><dt>Item name</dt><dd>{comparison.existing.name}</dd><dt>Description</dt><dd>{comparison.existing.description || "-"}</dd><dt>Packing</dt><dd>{comparison.existing.packagingPerBox}</dd><dt>Gross weight</dt><dd>{comparison.existing.grossWeightKg.toFixed(2)} kg</dd><dt>Department</dt><dd>{comparison.existing.departmentName}</dd></dl></div>
                  <div><p className="comparison-label uploaded-label">UPLOADED FILE</p><dl><dt>Item name</dt><dd>{comparison.uploaded.name || "-"}</dd><dt>Description</dt><dd>{comparison.uploaded.description || "-"}</dd><dt>Packing</dt><dd>{comparison.uploaded.packagingPerBox || "-"}</dd><dt>Gross weight</dt><dd>{comparison.uploaded.grossWeightKg ? `${comparison.uploaded.grossWeightKg.toFixed(2)} kg` : "-"}</dd><dt>Department</dt><dd>{comparison.uploaded.department || comparison.uploaded.departmentId || "-"}</dd></dl></div>
                </div>
                <div className="comparison-actions"><button className="secondary" onClick={() => reviewExisting(comparison.existing)}>Edit database values</button><button className="primary" onClick={() => reviewUploaded(comparison)}>Review uploaded values</button></div>
              </article>)}
            </div>}
            {customerDuplicateComparisons.length > 0 && <div className="duplicate-comparisons customer-duplicate-comparisons">
              {customerDuplicateComparisons.map((comparison) => <article className="duplicate-card" key={`${comparison.row}-${comparison.existing.id}`}>
                <div className="duplicate-card-heading"><strong>Row {comparison.row} / C_ID {comparison.uploaded.legacyId ?? comparison.existing.legacyId ?? comparison.existing.id}</strong><span>Existing database record vs uploaded record</span></div>
                <div className="customer-import-preview">
                  <table>
                    <thead><tr><th>Source</th><th>C_ID</th><th>C_Name</th><th>C_Add1</th><th>C_Add2</th><th>C_City</th><th>C_Pin</th><th>C_State</th><th>C_Country</th></tr></thead>
                    <tbody>
                      <tr><td className="comparison-label">Database</td><td>{comparison.existing.legacyId ?? comparison.existing.id}</td><td>{comparison.existing.name}</td><td>{comparison.existing.address1 ?? ""}</td><td>{comparison.existing.address2 ?? ""}</td><td>{comparison.existing.city ?? ""}</td><td>{comparison.existing.pincode ?? ""}</td><td>{comparison.existing.state ?? ""}</td><td>{comparison.existing.country ?? ""}</td></tr>
                      <tr><td className="comparison-label uploaded-label">Uploaded</td><td>{comparison.uploaded.legacyId ?? ""}</td><td>{comparison.uploaded.name ?? ""}</td><td>{comparison.uploaded.address1 ?? ""}</td><td>{comparison.uploaded.address2 ?? ""}</td><td>{comparison.uploaded.city ?? ""}</td><td>{comparison.uploaded.pincode ?? ""}</td><td>{comparison.uploaded.state ?? ""}</td><td>{comparison.uploaded.country ?? ""}</td></tr>
                    </tbody>
                  </table>
                </div>
                <div className="comparison-actions"><button className="secondary" onClick={() => reviewExistingCustomer(comparison.existing)}>Edit database values</button><button className="primary" onClick={() => reviewUploadedCustomer(comparison)}>Review uploaded values</button></div>
              </article>)}
            </div>}
            <div className="issue-list">
              {importIssues.map((issue, index) => (
                <div className="issue-row" key={`${issue.row}-${index}`}>
                  <strong>
                    Row {issue.row}
                    {issue.code ? ` / Item ${issue.code}` : ""}
                  </strong>
                  <span>{issue.message}</span>
                </div>
              ))}
            </div>
            <button
              className="primary dialog-ok"
              onClick={closeImportReview}
            >
              OK
            </button>
          </section>
        </div>
      )}
      {isLogoDialogOpen && (
        <div className="modal-backdrop" role="presentation" onMouseDown={() => setIsLogoDialogOpen(false)}>
          <section className="import-dialog logo-dialog" role="dialog" aria-modal="true" aria-labelledby="logo-dialog-title" onMouseDown={(event) => event.stopPropagation()}>
            <div className="dialog-heading">
              <div><p className="form-kicker">LABEL LOGO</p><h2 id="logo-dialog-title">Choose or upload logo</h2></div>
              <button className="dialog-close" type="button" onClick={() => setIsLogoDialogOpen(false)} aria-label="Close logo selection">X</button>
            </div>
            <label className="dialog-field">Available logos<select defaultValue=""><option value="">Uploaded logos</option>{labelLogos.map((logo) => <option key={logo.id} value={logo.id}>{logo.name}</option>)}</select></label>
            {labelLogos.length === 0 && <p className="dialog-summary">No uploaded logos are available yet. Add one below.</p>}
            <div className="dialog-actions"><button type="button" className="secondary" onClick={() => setIsLogoDialogOpen(false)}>Close</button></div>
            <form className="dialog-upload-form" onSubmit={uploadLabelLogo}>
              <p className="form-kicker">ADMIN UPLOAD</p>
              <label>Logo name<input value={logoUpload.name} onChange={(event) => setLogoUpload({ ...logoUpload, name: event.target.value })} placeholder="e.g. Nagreeka new" /></label>
              <label>Logo file<input type="file" accept=".png,.jpg,.jpeg,.svg,.webp,image/png,image/jpeg,image/svg+xml,image/webp" onChange={(event) => setLogoUpload({ ...logoUpload, file: event.target.files?.[0] ?? null })} /></label>
              <button type="submit" className="primary">Upload logo</button>
            </form>
          </section>
        </div>
      )}
      {isPackingPreviewOpen && dispatchResult && (
        <div className="modal-backdrop packing-backdrop" role="presentation" onMouseDown={() => setIsPackingPreviewOpen(false)}>
          <section className="packing-dialog" role="dialog" aria-modal="true" aria-labelledby="packing-preview-title" onMouseDown={(event) => event.stopPropagation()}>
            <div className="packing-dialog-actions no-print"><div><p className="form-kicker">DISPATCH DOCUMENT</p><h2 id="packing-preview-title">{dispatchPreviewKind === "packing" ? "Packing list preview" : "Sales order preview"}</h2></div><div><button type="button" className="secondary" onClick={dispatchPreviewKind === "packing" ? downloadPackingListSummaryPdf : downloadPackingListPdf}>Download PDF</button><button type="button" className="primary" onClick={dispatchPreviewKind === "packing" ? printPackingListSummary : printPackingList}>Print <span>-&gt;</span></button><button type="button" className="dialog-close" onClick={() => setIsPackingPreviewOpen(false)} aria-label="Close print preview">X</button></div></div>
            <article className="packing-sheet">
              {dispatchPreviewKind === "packing" ? <><h1 className="packing-list-title">Packing List</h1><div className="packing-so-date"><strong>SO No. : {dispatchResult.salesOrderNumber}</strong><time>{new Date().toLocaleDateString("en-GB", { day: "2-digit", month: "short", year: "2-digit" })}</time></div><section className="packing-ship-to"><strong>Ship To Address</strong><b>{dispatchResult.customer.name}</b>{packingAddressLines(dispatchResult.customer).map((line) => <span key={line}>{line}</span>)}</section><table className="packing-table packing-summary-table" style={{ border: "1px solid #202020" }}><thead><tr><th>Sr. No.</th><th>Item</th><th>Pcs in Each Box</th><th>Total No. of Boxes</th><th>Total No. of Pcs</th><th>Gross Wt of One box in (kgs)</th><th>Total Gross wt. (kgs)</th><th>Remark</th></tr></thead><tbody>{packingListLines.map((line, index) => <tr key={`${line.itemName}-${index}`}><td>{index + 1}</td><td>{line.itemName}</td><td>{line.piecesPerBox}</td><td>{line.boxes}</td><td>{line.totalPieces}</td><td>{line.grossWeightKg.toFixed(2)}</td><td>{line.totalGrossWeightKg.toFixed(2)}</td><td /></tr>)}</tbody></table></> : <><header className="packing-header"><div><strong>{dispatchResult.customer.name}</strong>{packingAddressLines(dispatchResult.customer).map((line) => <span key={line}>{line}</span>)}</div><time>{new Date().toLocaleDateString("en-GB", { day: "2-digit", month: "short", year: "2-digit" })}</time></header><div className="packing-meta"><div><span>Invoice No.</span><strong>{dispatchResult.invoiceNumber}</strong></div><div><span>SO No.</span><strong>{dispatchResult.salesOrderNumber}</strong></div><div><span>Dispatch date</span><strong>{dispatchResult.dispatchDate}</strong></div><div><span>Boxes</span><strong>{dispatchResult.dispatched}</strong></div></div><table className="packing-table" style={{ border: "1px solid #202020" }}><thead><tr><th>Invoice No.</th><th>SO No.</th><th>Dispatch Date</th><th>Batch No.</th><th>Item</th><th>Box No.</th></tr></thead><tbody>{dispatchResult.labels.map((label) => <tr key={label.barcodeValue}><td>{dispatchResult.invoiceNumber}</td><td>{dispatchResult.salesOrderNumber}</td><td>{dispatchResult.dispatchDate}</td><td>{label.batchNumber}</td><td>{label.itemName}</td><td>{label.barcodeValue}</td></tr>)}</tbody></table></>}
            </article>
          </section>
        </div>
      )}
    </main>
  );
}

export default App;
