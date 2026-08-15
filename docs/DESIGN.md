# Fakturama Image-to-Cash — Design Document

*Part 1 deliverable. No code required; this describes how the system is built and why.*

## 1. Goal

Turn a single order image into a saved, verified **Order** and linked **Invoice** in Fakturama:
extract structured data, resolve or create the **Debtor** and each **Product** (and VAT rate),
build the Order, generate a linked Invoice, apply the payment status — verifying each step
before moving to the next, without hardcoded coordinates or a fixed UI layout.

## 2. System overview

Three loosely-coupled layers:

1. **Extraction layer** (`IOrderExtractor`) — image → `OrderInfo`.
2. **Automation layer** (`IFakturamaAutomation`) — the Fakturama UI surface.
3. **Flow orchestrator** (`ImageToCashFlow`) — the stage machine that drives 1 and 2 and
   verifies each step.

The flow logic depends only on the two interfaces, so it can be unit-tested with a fake
automation and later pointed at a live app or a full model-based driver without change.

## 3. Control-discovery / grounding strategy

Fakturama is an **Eclipse RCP / SWT (Java)** desktop app. On Windows, SWT widgets map to native
Windows controls, so **Microsoft UI Automation (FlaUI, UIA3 backend)** can enumerate the control
tree. The grounding strategy:

- **Locate controls by identity, not coordinates**: match on `Name`, `ControlType`, and
  `ClassName`, plus **label adjacency** (`EditNearLabel`). **AutomationIds are deliberately
  avoided** — Fakturama reports a different id for the same field on every launch (e.g. the Date
  edit was `264324` one run, `592182` the next), so they are unstable. Coordinates are used only
  as *last resort* and never stored as layout constants. This is the direct answer to "no
  hardcoded coordinates / fixed layout".
- **Wait for existence**: `ControlQuery.WaitFor` polls until a control appears. Eclipse RCP
  lazily creates editors, so controls (e.g. the New Order editor's Date field) are not present
  until the editor opens.
- **Wait for stability on async lists**: search selectors populate asynchronously. We read the
  list content repeatedly and only trust it once consecutive reads are identical
  (`ControlQuery.WaitStable`) — this implements the "wait for the list to stabilize" requirement.
- **Verification by read-back**: after an action, we read the control's value (Value/Selection
  pattern) or OCR its bounding rectangle back to confirm the on-screen result, satisfying
  "verify each step before moving on".
- **Windows are brought on-screen first**: Eclipse windows can restore off-screen
  (`-25600,-25538`); we set a visible position before interacting/screenshotting.

### Discovered control map (this machine)
New Order (top toolbar `Create: New Order`), Save (`Save the current contents`), Date (Edit
adjacent to the `Date` label), Cust.Ref. (Edit named `Cust.Ref.`), No. (Edit adjacent to `No.`),
totals (Edits by name `Total Gross` / `VAT` / `Total`), follow-up Invoice button (Button `Invoice`
in the `Create a follow-up document` group — never the top-toolbar `Invoice`), select-existing
Debtor icon (upper icon next to the Customer field — never the green `+`).

Note: the concrete AutomationIds observed during discovery (Date `264324`, Cust.Ref. `133388`,
No. `198772`, Gross `133272`, VAT `67868`, Total `67872`, follow-up group `133290`, debtor icon
`133274`) are **unstable across launches** and must not be relied upon.

## 4. Image-extraction strategy

- **OCR**: Windows built-in OCR (WinRT `Windows.Media.Ocr`) returns words with bounding boxes —
  offline and free.
- **Layout-relative normalization**: labeled fields are parsed by `Label:` prefix; the items
  table is parsed by detecting the **column header anchors** (SKU/Description/Qty/Unit/VAT/Disc/
  Line) from the OCR output itself, then grouping body words into rows by Y-proximity and
  assigning each word to the nearest column anchor. This is robust to OCR reordering and
  column fragmentation — it never hardcodes absolute pixel positions.
- **Arithmetic self-check**: recompute net/VAT/gross from items and compare to the source
  totals; a missing qty is reconstructed from the line total (`lineTotal / (unit×(1−disc))`).
- **LLM-ready**: `IOrderExtractor` is the seam where a vision LLM can replace the heuristic
  normalizer when an API key is available.

## 5. The five-stage flow (with verification gates)

1. **Extract + open**: extract `OrderInfo`; open New Order; leave proposed No.; set Date,
   Cust.Ref.; price mode Net / VAT With VAT.
2. **Debtor**: search existing via the upper selector icon; **exact match** requires Company,
   First Name, Name, ZIP, City to all match. Ambiguous → **manual review** (stop). None →
   create (addresses, invoice/delivery roles, alias, discount 0, Net, payment method/terms),
   save, return to the Order, re-search and select.
3. **Products** (per item, in source order): search exact SKU → select; ambiguous → manual
   review; none → ensure VAT exists (Standard rate, exact %), then create product with
   gross = net×(1+VAT/100), cost 0, stock 0; save; re-select; complete the line
   (Qty/U.Price/Discount) and verify `price = qty × unit × (1 − disc/100)`.
4. **Save + verify Order** in Data → Documents.
5. **Linked Invoice**: use the Order's **"Create a follow-up document"** → Invoice (never the
   top-toolbar Invoice, to preserve the Order relationship); set payment method; apply paid
   status/date/value only when PAID; save; verify both the Invoice and the still-open source
   Order in Documents.

## 6. Tradeoffs

| Choice | Why | Cost / tradeoff |
|---|---|---|
| C# + FlaUI (UIA3) | Robust native UIA for SWT; first-class patterns | Java-side, plus separate OCR/LLM pipeline in same process |
| Windows OCR (offline) | Free, no API key, works today | More fragile than an LLM on messy/photo images |
| Heuristic normalizer (LLM-ready seam) | Deterministic, testable now | Needs tuning per layout variant; qty/SKU OCR noise |
| Interface-driven flow | Unit-testable decisions (manual-review gates) without live app | Extra abstraction layer |
| Dry-run default | Never corrupts the Fakturama DB during development | Full live path requires explicit opt-in + scratch DB |
| Identity-based grounding + stable-wait | No coordinates; async-safe | Requires control discovery per Fakturama version |

## 7. Verification strategy

- Unit tests for extraction, arithmetic, and the flow's exact-match / manual-review / dry-run
  decision logic.
- Live dry-run drives Fakturama (open order, fill header) and captures annotated screenshots at
  each stage; totals are read back and compared.
- Remaining live verification (persisted Documents rows) is the primary follow-up (see README,
  "If I had 3 more hours").
