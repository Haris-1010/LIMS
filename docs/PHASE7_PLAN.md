# Phase 7 plan: sample collection, barcodes and tracking

_Status: **plan only, awaiting approval**. No Phase 7 code has been written. Prepared 2026-11-09 after the Phase 6 audit._

## 1. Goal and scope

Move each ordered test from "paid for" to "physically in the right hands, correctly labelled, and traceable".

**In scope** (from the master requirements):
1. Sample identification with unique barcodes, and barcode label printing.
2. Sample collection with who/when recorded, and an identity check.
3. Status tracking and a complete history per sample.
4. Receiving samples in the laboratory (including scanning with a USB scanner).
5. Rejection with a reason, and recollection.
6. Transfers between branches (collection centre to main laboratory).
7. Sample-to-order and sample-to-test relationships.

**Out of scope** (later phases): worklists and result entry (Phase 8), reports (Phase 9), storage and disposal of retained samples, analyzer barcodes (Phase 14), SMS/WhatsApp to patients, a label designer.

## 2. What already exists and must be preserved

| Existing piece | How Phase 7 uses it |
|---|---|
| `lab.Orders` / `lab.OrderItems` with `OrderItemStatus` (Ordered 0, **Collected 10, Received 20**, InProgress 30, Completed 40, Cancelled 90) | Statuses 10 and 20 were reserved for this phase. A test's status follows its sample. |
| Phase 6 rule "a collected test can't be cancelled" (`line_in_progress`) | Becomes real once samples are collected. Unchanged. |
| `Tests.SampleTypeId`, `SampleTypes` (container, cap colour), 16 seeded types | Decides which tests share one tube. |
| Number sequence type `Sample` (prefix `S`, resets daily, e.g. `S-261109-0001`) | Barcode value. |
| Permissions `samples.view / collect / receive / reject / transfer` (already in the catalog and the menu) | No new permissions are strictly required. |
| Menu entries Collection, Tracking, Rejections (currently "planned for Phase 7") | Become real pages. Two entries are added: Receiving and Transfers. |
| Patterns: composite organisation foreign keys, `RowVersion`, audit interceptor, `BusinessRuleException` codes translated in the UI, idempotent actions, `DataTable`/`Modal` components | Reused as they are. |

## 3. Core design

### 3.1 Sample model
- A **sample** is one physical tube or container for **one order**, of **one sample type**, covering one or more tests of that order. Example: CBC and ESR (both EDTA) share one tube; LFT (serum) gets a second.
- Samples are **planned automatically when the order is created**, inside the same transaction, so labels can be printed at the front desk and every order always has its samples. Each gets a barcode from the `Sample` sequence.
- Sample-to-test links live in `lab.SampleTests`, so history survives recollection.

### 3.2 Statuses and allowed moves

```
Planned ──collect──► Collected ──receive──► Received ──(Phase 8: processing)
   │                    │  ▲                   │
   │                    │  └─cancel dispatch   └──reject──► Rejected
   │                    └─dispatch─► InTransit ─receive─► Received
 cancel                              (to another branch)
(all tests cancelled)                                      Rejected / Cancelled are final
```

| Status | Value | Meaning |
|---|---|---|
| Planned | 0 | Barcode exists; specimen not yet taken |
| Collected | 10 | Taken from the patient; held at the collecting branch |
| InTransit | 15 | On a transfer manifest to another branch |
| Received | 20 | Accepted by the laboratory |
| Rejected | 80 | Unsuitable; may have a replacement sample |
| Cancelled | 90 | Every test on it was cancelled before collection |

The server is the only authority on moves; every action checks the current status and uses `RowVersion`, so two people acting on the same tube cannot both succeed.

### 3.3 Rejection and recollection
- A reason from a configurable list is mandatory (some reasons also require a note).
- By default rejection **creates a replacement sample**: new barcode, status Planned, same tests, linked to the rejected one. The tests return to "Ordered" and appear in the collection queue marked "Recollection".
- The patient is contacted by staff (phone shown). No automatic messaging.

### 3.4 Branch transfers and visibility
- A **transfer manifest** lists samples sent from one branch to another (number from a new `Transfer` sequence). The sender scans samples to dispatch; the receiver scans each to accept; unreceived samples are flagged as missing when the manifest is closed.
- **Visibility rule (decision needed, see section 9).** The Phase 6 branch filter hides other branches' records, which would stop the main laboratory from seeing a collection centre's samples. Proposed rule for samples only: a user sees a sample if their branch access includes its **origin**, **current** or **destination** branch. It is applied in one place (`SampleService`), covered by tests, and guarded by an architecture test. Phase 8 will rely on the same rule.

### 3.5 Barcodes and scanners
- Format: **Code 128** carrying the sample number (for example `S-261109-0001`), readable by any USB scanner (they type the code and press Enter).
- **No new library.** The encoder runs on the server (C#, unit-tested with a decoder round trip); the API returns the bar pattern and the browser only draws it as SVG. Fully offline.
- Lookups ignore case and stray whitespace, and a scan field is focused on every sample screen.
- Printing uses the browser and the label printer's normal driver. Default label **50 mm × 25 mm** (barcode, number, patient name and number, age/sex, tube type with colour, test short names, branch, date). Size and copies are remembered per workstation. Patient names print in English on labels (Nastaliq is unreadable at label size).
- Every print and reprint is logged in the sample history.

## 4. Database changes (migration `AddSamples`)

All tables carry `OrganizationId` with composite foreign keys, as in earlier phases. No cascading deletes.

| Table | Purpose and key constraints |
|---|---|
| `lab.Samples` | OrderId, PatientId, SampleTypeId, **Barcode** (unique per organisation), Status, Priority (copied from the order), OriginBranchId, CurrentBranchId, DestinationBranchId, collection fields (at, by, identity confirmed, notes), receive fields, rejection fields (reason, note, at, by), `ReplacesSampleId`, label print count, `RowVersion`. Checks: valid status; a collected sample records collector and time; a received one records receiver and time; a rejected one records a reason. **Filtered unique index: at most one live sample per (order, sample type)**, which enforces the grouping rule. |
| `lab.SampleTests` | (SampleId, OrderItemId) links a sample to its tests. |
| `lab.SampleEvents` | Append-only history: type (Planned, LabelPrinted, Collected, Dispatched, Received, Rejected, Recollection requested, Cancelled, Transfer cancelled…), time, user, branch, note. This is the "complete sample history". |
| `lab.SampleTransfers`, `lab.SampleTransferItems` | Manifests and their samples. Checks: from-branch differs from to-branch; status valid. |
| `lab.SampleRejectionReasons` | Per-organisation list: code, English/Urdu names, requires-note flag, allows-recollection flag, active. |

Other changes:
- New sequence type `Transfer` (prefix `TRF`, monthly), created on first use like `Refund` was.
- **Backfill:** a safe, repeatable step plans samples for orders created before this phase (the dev database has a single cancelled order).
- Default rejection reasons are **operational, editable and not clinical rules**: haemolysed, clotted, insufficient volume, wrong container, unlabelled or mislabelled, leaked or broken, delayed transport, contaminated, wrong patient, other (10 reasons). The laboratory should confirm the list.
- Existing organisations get the default reasons through the migration; new ones through provisioning.

## 5. API (all under the existing cookie, CSRF and permission rules)

| Endpoint | Permission | Purpose |
|---|---|---|
| `GET /api/samples` | samples.view | Paged list: status, branch, dates, barcode/patient/order search |
| `GET /api/samples/{id}`, `GET /api/samples/by-barcode/{code}` | samples.view | Detail with history; scan lookup. Returns the actions the caller may take now |
| `GET /api/orders/{id}/samples` | orders.view + samples.view | Samples of an order |
| `GET /api/samples/collection-queue` | samples.collect | Planned samples at the working branch, urgent first, with waiting time |
| `POST /api/samples/{id}/collect`, `POST /api/orders/{id}/samples/collect` | samples.collect | Collect one or all samples of an order. Requires the identity confirmation |
| `POST /api/samples/{id}/receive` | samples.receive | Receive at the working branch |
| `POST /api/samples/{id}/reject` | samples.reject | Reason, note, "request recollection" |
| `POST /api/samples/{id}/labels/printed` | collect or receive | Records print or reprint (with a reason for reprints) |
| `POST /api/sample-transfers` | samples.transfer | Create and dispatch a manifest |
| `GET /api/sample-transfers`, `GET /api/sample-transfers/{id}` | samples.transfer or receive | Outgoing and incoming lists, progress |
| `POST /api/sample-transfers/{id}/receive` | samples.receive | Scan-receive one sample at the destination |
| `POST /api/sample-transfers/{id}/complete`, `…/cancel` | samples.transfer / receive | Close with missing list; cancel before anything is received |
| `GET/POST/PUT /api/samples/rejection-reasons` | view: samples.view; edit: catalog.manage | Reason list |

Changes to Phase 6 code (small, inside existing transactions): `OrderService.CreateAsync` plans the samples; cancelling a test or order updates its sample (or cancels the sample when no tests remain). The order detail response is unchanged; samples come from their own endpoint.

## 6. Screens

| Screen | What it does |
|---|---|
| **Collection** (`/samples/collection`) | Scan box plus a queue of planned samples (patient, tests, tube colour, priority, waiting time). Opening one shows a large patient banner (name, age, sex, number), the tests and tube, a **required identity-confirmed checkbox**, notes, Collect / Reject. Keyboard friendly: scan, tick, Enter. |
| **Receiving** (`/samples/receiving`, new) | Large scan field: each scan receives a sample at once and adds it to a running list. Clear messages for unknown, not-yet-collected, already received, or in transit to another branch. |
| **Tracking** (`/samples/tracking`) | Search by barcode, patient or order; status and date filters; opens the detail with the full **timeline**, tests, patient, location and the actions the user may take. |
| **Rejections & recollection** (`/samples/rejections`) | Rejected samples with reason, who, patient phone, and the replacement's status; reason list management for administrators. |
| **Transfers** (`/samples/transfers`, new) | Outgoing and incoming tabs; build a manifest by scanning; print the manifest; receive by scanning with a progress count; close with missing samples listed. |
| **Labels** | Preview and print dialog from the order page, collection, detail and right after an order is created; batch printing with copies. |
| **Order detail** | New "Samples" card: barcodes, status, print labels, collect all. |

All screens are bilingual with right-to-left support. Barcodes and labels stay left-to-right.

## 7. Permissions and roles

No new permission keys. Suggested defaults to review:

| Role | Today | Suggested change for new organisations |
|---|---|---|
| Phlebotomist | view, collect, reject | add **transfer**, so collection centres can dispatch |
| Technician | view, receive, reject, transfer | none |
| Receptionist | view | none (prints labels through order creation) |
| Lab Manager / Administrator | all | none |

Existing organisations keep their roles; administrators can adjust them on the Roles page.

## 8. Test plan

**Unit tests**
- Code 128 encoder: start/stop patterns, checksum, round-trip decode of many sample numbers, rejection of unsupported characters.
- Status machine: every allowed and forbidden move.
- Sample grouping: same type shares a tube, different types split, packages expand.
- Barcode normalisation (case, spaces, line endings).

**Integration tests (real API and SQL Server)**
1. Order creation plans the right samples with unique barcodes, atomically; nothing is left behind if the order fails.
2. Cancelling a test before collection updates or cancels its sample; after collection it is refused.
3. Collect: permissions, identity confirmation required, collector and time set by the server, double scan returns an informative conflict, **parallel collects give exactly one winner**, test statuses follow.
4. Receive: same branch, from transfer, uncollected refused, double receive.
5. Reject: reason required, note when needed, replacement created with a new barcode, tests back to Ordered, replacement collectable, repeated rejection chain, terminal states.
6. Transfers: only collected samples at the sender's branch, a sample cannot be in two manifests (race), only the destination receives, missing samples on close, cancel rules.
7. **Isolation:** another organisation gets 404 on barcodes and lists; branch visibility rule for origin, current and destination; users without access see nothing.
8. **Races:** collect vs reject vs cancel on the same sample leave sample and test statuses consistent.
9. Database guards through raw SQL: duplicate barcode, two live samples of one type per order, invalid status, missing collector, cross-organisation links.
10. Backfill is repeatable (running twice creates nothing new).
11. Every new error code has an English and Urdu message (existing translation test).
12. **Regression:** all 153 existing tests keep passing, including the Phase 6 audit suite.

**Browser checks:** the full journey (order, print labels, collect by scan, transfer, receive, reject, recollect) in English and Urdu; keyboard-only use; label preview dimensions; a scanner simulated as fast typing plus Enter; the receptionist, phlebotomist and technician views.

## 9. Decisions needed from you

| # | Question | My recommendation |
|---|---|---|
| 1 | **Visibility rule** for samples across branches (3.4) | Approve the origin/current/destination rule |
| 2 | May a sample be collected **before the invoice is paid**? | Yes by default, with an organisation setting to require payment first |
| 3 | Label printer and size (50×25 mm assumed). Which printer model? | Confirm size; any printer with a Windows driver works |
| 4 | Barcode text: `S-261109-0001` (readable, includes the date) or numeric only? | Keep as is; Code 128 handles it |
| 5 | **Rejection reasons**: is the default list in section 4 acceptable? | Approve as a starting list; your staff edit it |
| 6 | Do collection centres send samples to the main laboratory **by manifest**? Should Phlebotomists be allowed to dispatch? | Yes to both |
| 7 | One tube per sample type per order is acceptable? (Splitting one type across departments would need per-test sample requirements.) | Yes for now |
| 8 | Identity-confirmed checkbox mandatory at collection? | Yes |

## 10. Delivery sequence and risks

| Step | Content | Verified before moving on |
|---|---|---|
| 7.1 | Domain, migration, plan-on-order hook, status rules | unit and integration tests, Phase 6 regression |
| 7.2 | Collect, receive, reject and recollection APIs | permission, race and isolation tests |
| 7.3 | Transfers | manifest and race tests |
| 7.4 | Barcode encoder, label printing, collection screen | encoder round trip, browser check |
| 7.5 | Receiving, tracking, rejections, transfers screens, reason management | browser check in both languages |
| 7.6 | Order page integration, translations, docs, a self-audit like Phase 6 | full suite, build, lint |

| Risk | Mitigation |
|---|---|
| The visibility rule is the only place that bypasses the Phase 6 branch filter | One code path, explicit tests, architecture test |
| Touching order creation and cancellation could regress Phase 6 | Small hooks inside existing transactions; the 153 tests plus new regression tests run at every step |
| Label printing differs between printers | Printer-agnostic browser printing, configurable size, preview, tested on the actual printer you use before sign-off |
| Scan speed and double scans | Server-side state checks, informative conflicts, idempotent receiving |

**Estimated size:** roughly the size of Phases 5 and 6 combined: about 6 tables, 20 endpoints, 6 screens, 120 new tests.
