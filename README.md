# Fakturama Image-to-Cash Automation

Turns a single order image into a fully saved, verified **Order** and linked **Invoice**
inside [Fakturama](https://www.fakturama.info/) — extracting the source data, resolving or
creating the Debtor and Product master records, and applying the correct payment status —
**without hardcoded coordinates or a fixed UI layout.**

Stack: **C# / .NET 10 (Windows)** · **FlaUI (UIA3)** · **Windows OCR (WinRT)** ·
layout-relative heuristic extraction (LLM-ready).

---

## Architecture

```
Fakturama.ImageToCash.sln
├─ src
│  ├─ ImageToCash.Core          domain models, IOrderExtractor, IFakturamaAutomation
│  ├─ ImageToCash.Extraction    WindowsOcrEngine + HeuristicOrderExtractor
│  ├─ ImageToCash.UiAutomation  FakturamaSession, ControlQuery (grounding), FakturamaDriver
│  ├─ ImageToCash.Flow          ImageToCashFlow (stages 1–5), StepLog, dry-run/live
│  └─ ImageToCash.Console       CLI: extract / probe / openorder / run
├─ tests/ImageToCash.Tests      xUnit: extraction + arithmetic + flow (14 tests)
├─ samples/order-sample.png     synthesized order image (test input)
├─ tools/synthesize_order.py    regenerates the sample image
└─ docs/DESIGN.md               Part 1 design document
```

**Key idea — decouple the flow from the UI.** The flow logic (`ImageToCashFlow`) talks only
to the `IFakturamaAutomation` abstraction, so it is fully unit-tested with a fake automation.
The real `FakturamaDriver` implements that interface over FlaUI/UIA. The extractor is also an
interface (`IOrderExtractor`), so an LLM vision extractor can replace the heuristic one later.

---

## Setup

1. **Prerequisites**
   - Windows 10+ with .NET SDK 10 (this repo was built with `10.0.200-preview`).
   - Fakturama2 installed (default `C:\Program Files\Fakturama2\Fakturama.exe`).
   - NuGet restore is online (FlaUI packages).
2. **Build**
   ```powershell
   dotnet build Fakturama.ImageToCash.slnx
   ```
3. **Regenerate the sample order image (optional)**
   ```powershell
   python tools/synthesize_order.py
   ```

---

## Running

All modes attach to a running Fakturama or launch it. Launch Fakturama first, or use `--launch`.

```powershell
# 1. Extract structured data from an order image (no Fakturama needed)
dotnet run --project src/ImageToCash.Console -- extract --image samples/order-sample.png

# 2. Dump Fakturama's UIA control tree (grounding / control discovery)
dotnet run --project src/ImageToCash.Console -- probe --attach <pid> --depth 10

# 3. Open a New Order and dump its editor tree (control discovery)
dotnet run --project src/ImageToCash.Console -- openorder --attach <pid>

# 4. Full Order-first flow — DRY RUN (safe: no writes, captures annotated screenshots)
dotnet run --project src/ImageToCash.Console -- run --image samples/order-sample.png `
    --attach <pid> --shot-dir artifacts/shots

# 5. Full flow — LIVE (persists Order + Invoice + payment; use on a scratch DB)
dotnet run --project src/ImageToCash.Console -- run --image samples/order-sample.png `
    --attach <pid> --live
```

> **Safety:** the default is **dry-run**. It navigates and fills fields but never calls Save,
> never creates master records, and never writes payment. Use `--live` only against a test
> Fakturama database (the machine used for this work pointed Fakturama at a scratch DB under
> `D:\TJM Labs\Test Fakturama`).

---

## How the flow maps to the assessment (stages 1–5)

`ImageToCashFlow.RunAsync` implements the five stages with a verify-before-advance checkpoint
on every step:

1. **Extract** image → `OrderInfo` (order date, external ref, debtor, payment, items, totals).
   Self-checks computed totals against source totals.
2. **Open New Order**; leave proposed No.; set Date, Cust.Ref.; price mode Net / VAT With VAT.
3. **Debtor** — search existing (exact-match gate: Company/Name/ZIP/City); on ambiguity stop
   for **manual review**; if absent create then re-select. Keeps the Order tab open throughout.
4. **Products** — per item in source order: search exact SKU → select; ambiguity stops; absent
   → create (gross = net × (1 + VAT/100), cost 0, stock 0), re-select, complete the line
   (Qty / U.Price / Discount) and verify line price.
5. **Save Order**; create **linked Invoice via "Create a follow-up document"** (preserves the
   Order relationship, never the top-toolbar Invoice button); apply payment status (only set
   date/value when PAID); save; verify both rows in Data → Documents.

The exact-match decision logic and the manual-review gates are fully unit-tested
(`tests/ImageToCash.Tests/FlowTests.cs`).

---

## Grounding strategy (control discovery)

Fakturama is an **Eclipse RCP / SWT (Java)** app. On Windows, SWT maps to native controls, so
FlaUI/UIA3 can read the tree. We match controls by **AutomationId / Name / ControlType** — never
coordinates — and use a **poll-until-stable** waiter for async lists
(`ControlQuery.WaitStable`). Key controls discovered on this machine (see `docs/DESIGN.md`):

| Control | How we find it |
|---|---|
| New Order (top toolbar) | Button `Create: New Order` |
| Save | Button `Save the current contents` |
| Date / Cust.Ref. / No. | Edits `264324` / `133388` / `198772` |
| Totals (Gross / VAT / Total) | Edits `133272` / `67868` / `67872` |
| Follow-up Invoice | Button `Invoice` in group `133290` |
| Select-existing Debtor icon | Image `133274` (never the green `+`) |

---

## Tests

```powershell
dotnet test Fakturama.ImageToCash.slnx
```

Coverage: extraction normalization (debtor/payment/items/totals, qty reconstruction),
price/total arithmetic, and the flow decision layer (dry-run skips writes; live executes;
ambiguous debtor/product → manual review; missing debtor → create; screenshot capture).

---

## Remaining work (see docs/DESIGN.md)

The full **live** master-data creation and the deep modal dialogs (address selector, product
selector, VAT/terms-of-payment editors) are the parts not yet driven live in this timebox. The
exact-match **decision logic** is implemented and tested; the **dialog interaction** is scaffolded
in `FakturamaDriver` as clearly-marked best-effort methods. See **"If I had 3 more hours"** below.

### If I had 3 more hours, what would I do?
1. **Drive the modal dialogs live** — implement `FindExistingDebtor` / `FindExistingProduct`
   end-to-end: open the selector, type the query, `WaitStable` on the result list, apply the
   exact-match gate by reading Company/Name/ZIP/City and SKU rows, and select/OK.
2. **Implement the master-data creation editors** (`CreateDebtor`, `CreateProduct`) and the
   VAT/terms-of-payment conditional creation paths (payment-code mapping, Standard-rate VAT,
   gross-price math), each with save + return-to-Order + re-select verification.
3. **Complete the items grid** — drive the SWT table cells for Qty / U.Price / Discount and
   verify each line price, then confirm totals before Save.
4. **Add an LLM vision extractor** behind `IOrderExtractor` to remove the remaining OCR
   fragility (dropped digits like qty `1`, SKU `01`→`OI`, `%`→`0/0`) seen with Windows OCR.
5. **Hardening**: window-state persistence handling, screenshot OCR read-back for field
   verification, retries on Eclipse RCP control re-creation, and a `--live` end-to-end test on
   a clean scratch DB with assertions on the persisted Documents list.

---

## Repository

Initialized with Git (`main`). Run files and screenshots are written under `artifacts/` (ignored).
