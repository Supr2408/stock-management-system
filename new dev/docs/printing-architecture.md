# BoxTrack Printing Subsystem Architecture

## 1. Overview

The BoxTrack printing subsystem manages document and barcode label printing across the factory. In this first phase, the **Admin PC is also the Server PC**, and printers physically or networked to this machine are discovered and driven directly by the backend API.

The subsystem strictly separates printing into two independent operational categories:
1. **Regular Document Printing**: Normal documents, PDFs, stock summary reports, dispatch slips, and packing sheets.
2. **Barcode Printing**: Box barcode stickers, serial labels, and trace tags.

---

## 2. Architecture & Components

```text
React Frontend (More -> Printing)
         │
         ▼  (REST API with JWT Auth)
BoxTrack.Api (PrinterController)
         │
         ▼
BoxTrack.Application (IPrinterCatalog, IPrinterDiscoveryService, IPrintService)
         │
         ▼
BoxTrack.Infrastructure
 ├── WindowsPrinterDiscoveryService (System.Drawing.Printing.PrinterSettings.InstalledPrinters)
 ├── WindowsPrintService (System.Drawing.Printing.PrintDocument spooler)
 └── PrinterCatalogService (PostgreSQL persistence & AuditLog records)
         │
         ▼
Windows Print Spooler (Local / Networked Printers on Admin PC)
```

### Clean Layering & Abstractions

- **`IPrinterDiscoveryService`**:
  Abstracts discovering installed printers. On Windows, this enumerates `PrinterSettings.InstalledPrinters` and queries status/default attributes. Isolating discovery allows swapping in remote agent discovery later without altering application logic.
- **`IPrintService`**:
  Abstracts print job execution (`PrintDocumentAsync`, `TestPrintAsync`). Creates `PrintJob` entities in the database, tracks execution lifecycle (`Queued -> Printing -> Completed / Failed`), and handles printer hardware spooling.
- **`IPrinterCatalog`**:
  Coordinates printer configuration validation, saving, retrieval, and audit logging. Validates that chosen printers are genuinely present on the machine before committing changes.

---

## 3. Printer Categories & Database Configuration

Printers are configured independently for two distinct categories:

- **`PrinterCategory.RegularDocument`**
- **`PrinterCategory.Barcode`**

### Database Schema

1. **`printer_configuration`**:
   - `id`: Primary key.
   - `category`: Integer enum (`1 = RegularDocument`, `2 = Barcode`).
   - `printer_name`: Actual Windows printer name (e.g. `HP LaserJet M1005`, `Canon LBP2900`, `Zebra ZD220`).
   - `is_active`: Boolean flag indicating if this is the active printer.
   - `created_at`, `updated_at`, `created_by`: Audit timestamps and user tracking.

2. **`print_job`**:
   - `id`: Primary key.
   - `category`: `RegularDocument` or `Barcode`.
   - `printer_name`: Target printer.
   - `document_name`: Name or title of the print job.
   - `document_reference`: Optional file path or identifier (e.g. generated PDF).
   - `copies`: Number of copies requested.
   - `status`: `Queued`, `Printing`, `Completed`, `Failed`, `Cancelled`.
   - `requested_by`: User username.
   - `created_at`, `started_at`, `completed_at`: Timestamps.
   - `error_message`: Error text if spooling failed.

3. **`audit_log`**:
   Every time an administrator sets or updates a printer configuration, an audit record (`EntityName = "PrinterConfiguration"`, `Action = "Created" | "Updated"`) is created capturing `before_json` and `after_json`.

---

## 4. API Endpoints

All printer management endpoints are under `api/printers` and require Admin role authentication (`[Authorize(Roles = "Admin")]`):

- **`GET /api/printers/available`**:
  Returns installed printers on the Server PC with name, display name, status (`Ready`, `Offline`, `Unknown`), and `isDefault`.
- **`GET /api/printers/configuration`**:
  Returns active configuration summary for both `RegularDocumentPrinter` and `BarcodePrinter`.
- **`PUT /api/printers/configuration/regular`**:
  Updates the regular document printer. Validates existence against installed printers.
- **`PUT /api/printers/configuration/barcode`**:
  Updates the barcode printer. Validates existence against installed printers.
- **`POST /api/printers/test-print`**:
  Dispatches a test print to the configured printer for either `RegularDocument` or `Barcode`. Returns the generated `PrintJobDto`.
- **`GET /api/printers/jobs?limit=20`**:
  Lists recent print jobs with execution status and timestamps.

---

## 5. Frontend Navigation

- Located under **More** -> **Printing**.
- Features dedicated sub-tabs:
  - **A. Regular Document Print**: Current printer status, available printers dropdown, Save button, and Test Print button.
  - **B. Barcode Print**:
    - **Laser Printer**: Renders standard graphical barcode sticker layouts via PDF/document pipeline.
    - **TSC Printer**: Targeted at industrial and desktop thermal label printers like the **TSC TTP-247**. Features interactive **Barcode Label Template Editor**, dimension configuration, logo fitting, live preview canvas, and native TSPL/TSPL-EZ hardware test printing.
- Live scan: "Rescan PC printers" button to reload printer list without page refresh.
- Job Activity Table: Displays server print jobs with statuses, timestamps, and error messages.

---

## 6. TSC Barcode Engine Architecture (TSC TTP-247)

### TSPL / TSPL-EZ Command Language (NOT a CLI)
The TSC TTP-247 uses native **TSPL/TSPL-EZ** command language over raw printer connections. The printing subsystem does **NOT** invoke any shell CLI (e.g. `tsc.exe`), nor does it run `COPY CON LPT1`. Command generation and transport are fully modularized:

```text
LabelTemplate & Elements
         │
         ▼
ITscCommandGenerator (TscTsplCommandGenerator)
   ├── 203 DPI Metric Math: dots = round(mm * (203 / 25.4)) ≈ 8 dots/mm
   ├── Placeholder Resolution: {Barcode}, {ItemName}, {BatchNo}, etc.
   └── IImageProcessor (TscLogoProcessor): Contain / Cover / Stretch / Monochrome 1-bit Bitmap
         │
         ▼ (TSPL Binary Payload)
ITscLabelPrinter (TscLabelPrinter)
         │
         ▼
IPrinterTransport (WindowsRawPrinterTransport)
   └── Win32 Spooler P/Invoke: OpenPrinter, StartDocPrinter("RAW"), WritePrinter, EndPagePrinter, ClosePrinter
         │
         ▼
TSC TTP-247 Thermal Printer
```

### 203 DPI Dot Calculations
For 203 DPI printers (standard for TSC TTP-247):
$$\text{Dots} = \text{round}\left(\text{Millimeters} \times \frac{203}{25.4}\right)$$
This gives approximately **8 dots per mm** ($1\text{ mm} \approx 7.9921\text{ dots}$). All label template elements store physical dimensions in millimeters ($mm$) in PostgreSQL, and coordinates are dynamically converted to dots at generation time.

### TSPL Commands Generated
- `SIZE width mm, height mm`: Sets the label paper physical size.
- `GAP gap mm, offset mm`: Sets the vertical gap between labels.
- `DIRECTION 0|1`: Sets print feed orientation.
- `CLS`: Clears the printer image buffer.
- `BOX x,y,x_end,y_end,line_thickness`: Draws precise borders and separator lines.
- `TEXT x,y,"font",rotation,x_mul,y_mul,"content"`: Draws text with hardware font scaling.
- `BARCODE x,y,"type",height,readable,rotation,narrow,wide,"content"`: Generates 1D hardware barcodes (Code 128, Code 39, EAN13).
- `BITMAP x,y,width_bytes,height,mode,bitmap_data`: Burns 1-bit monochrome graphics (mode 0, overwrite).
- `PRINT sets,copies`: Executes thermal transfer / direct thermal print feed.

### Logo & Image Processing Pipeline
1. Uploaded logos (`LabelLogo`) are processed via `TscLogoProcessor`.
2. Aspect ratio handling supports:
   - **Contain**: Scales logo to fit completely within target width/height without distortion.
   - **Cover / Crop**: Scales logo to completely cover target bounds, centering and clipping overflow.
   - **Stretch**: Fills exact rectangular dimensions.
3. Pixel conversion:
   - Converts to grayscale using luminance formula: $Y = 0.299R + 0.587G + 0.114B$.
   - Binarizes via thresholding (128 luminance).
   - Packs into 1-bit per pixel byte streams aligned to bytes per row: $\text{WidthBytes} = \lceil\text{WidthDots} / 8\rceil$.

---

## 7. Laser Barcode Multi-Label A4 Sheet Compositor

### Single Label Template for Both TSC and Laser
BoxTrack defines each barcode label template **once** in the database (`LabelTemplate` with `LabelTemplateElement` children). The same template is processed through two dedicated renderers:
1. **TSC Mode**: Renders through `TscTsplCommandGenerator` into raw TSPL/TSPL-EZ commands for industrial thermal printers.
2. **Laser Mode**: Renders through `LaserA4LabelCompositor` (`ILaserLabelCompositor`), which dynamically tiles multiple individual barcode labels onto physical A4 sheets ($210\text{ mm} \times 297\text{ mm}$). **It never prints 1 label per PDF page.**

### A4 Page Geometry Math
Given paper dimensions, margins, and gaps configured by the administrator in `PrinterConfiguration`:
- $\text{UsableWidth} = \text{PaperWidthMm} - \text{MarginLeftMm} - \text{MarginRightMm}$
- $\text{UsableHeight} = \text{PaperHeightMm} - \text{MarginTopMm} - \text{MarginBottomMm}$
- $\text{Columns} = \left\lfloor \frac{\text{UsableWidth} + \text{HorizontalGapMm}}{\text{LabelWidthMm} + \text{HorizontalGapMm}} \right\rfloor$
- $\text{Rows} = \left\lfloor \frac{\text{UsableHeight} + \text{VerticalGapMm}}{\text{LabelHeightMm} + \text{VerticalGapMm}} \right\rfloor$
- $\text{LabelsPerPage} = \text{Columns} \times \text{Rows}$

### Tiling & Pagination
For $N$ labels to print:
- $\text{PageCount} = \lceil N / \text{LabelsPerPage} \rceil$
- Label $i$ ($0 \le i < N$) is mapped to:
  - $\text{PageIndex} = \lfloor i / \text{LabelsPerPage} \rfloor$
  - $\text{SlotInPage} = i \pmod{\text{LabelsPerPage}}$
  - $\text{Col} = \text{SlotInPage} \pmod{\text{Columns}}$
  - $\text{Row} = \lfloor \text{SlotInPage} / \text{Columns} \rfloor$
  - $X = \text{MarginLeftMm} + \text{Col} \times (\text{LabelWidthMm} + \text{HorizontalGapMm})$
  - $Y = \text{MarginTopMm} + \text{Row} \times (\text{LabelHeightMm} + \text{VerticalGapMm})$

For example, standard $50\text{ mm} \times 30\text{ mm}$ labels on A4 with $10\text{ mm}$ margins and $2\text{ mm}$ gaps yield **3 columns $\times$ 8 rows = 24 labels per sheet**.

### High-Fidelity Vector Rendering
- Barcodes are rendered as crisp vector line strips (GDI+ / SkiaSharp vector primitives) to ensure 100% optical scanner readability on 600 DPI laser printers.
- Logos and graphics are resampled at 300 DPI high-quality bicubic interpolation using the template element's configured aspect ratio mode (`Contain`, `Cover`, `Stretch`).
- Borders and boxes are drawn with fractional line widths matching configured thickness.

---

## 8. Role-Based Authentication & Operational Scoping

BoxTrack implements a single unified login page (`/api/auth/login`) with three distinct operational roles:

| Role | Username Pattern | Scope & Permissions | Restricted Features |
| :--- | :--- | :--- | :--- |
| **Admin** | `admin` | Full unrestricted access across all departments, Master data, Customer CUD, Label generation, Duplicate labels, Production, Dispatch, User accounts, and Printer subsystem. | None |
| **Production** | Department name (e.g. `afc`, `afr`, `plascon`, `assembly`) | Department-scoped. Only sees and operates on pending production, stock receipts, and reports for their assigned department. Backend strictly enforces this via `CurrentUser.DepartmentId`. | Cannot access Master, Customer, Label generation, Duplicate labels, Dispatch, Users, or Printer config. |
| **QC** | `qc` | Cross-department quality control. Accesses Production, Label generation, Dispatch, and Reports across all departments. | Explicitly blocked from "Print duplicate label", User management, and Printer configuration. |

### Development Passwords Notice
> [!WARNING]
> **DEVELOPMENT USE ONLY — MUST BE REMOVED BEFORE PRODUCTION DEPLOYMENT**
> 
> Temporary passwords (e.g., `123` for Production and QC accounts, `Password123!` for Admin) are seeded into the database and displayed exclusively on the **Admin -> User accounts** desk for developer convenience.
> Passwords are never logged in application logs and are never embedded in JWT authentication tokens.

---

## 9. Current Operational Model & Limitations

- **Co-located Server & Admin PC**:
  The backend API runs directly on the Admin PC where the physical or USB/network printers are installed.
- **Server Discovery & Spooling**:
  The browser communicates only with the backend API. All raw printer communication is executed safely server-side via the Windows Print Spooler RAW data type.

---

## 10. Future Extensibility: Department Printer Agents

In subsequent phases when BoxTrack scales to multiple departments, printers may be physically attached to departmental client PCs rather than the central server:

```text
               Central BoxTrack Server
                         │
        ┌────────────────┼────────────────┐
        ▼                ▼                ▼
   Admin PC        Department A       Department B
 Printer Agent     Printer Agent      Printer Agent
        │                │                │
 [Office Laser]     [Zebra ZD220]    [TSC TTP-247]
```

Because `IPrinterDiscoveryService`, `IPrintService`, `ITscCommandGenerator`, `ILaserLabelCompositor`, and `IPrinterTransport` are completely decoupled interfaces, remote agents (via SignalR / gRPC / MQTT) can easily implement `IPrinterTransport` without modifying domain entities or frontend components.


