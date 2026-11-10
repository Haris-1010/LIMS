# Phase 8 plan: worklists, result entry, verification, approval, critical alerts

_Status: **approved and built** (see `IMPLEMENTATION_STATUS.md`). All ten proposed defaults were approved as written._

## 1. Scope

**In scope**
- Worklists: the queue of tests whose sample has been received, and saved worklists (batches) with a printable sheet.
- Result entry per test, per parameter: numeric, option, text and narrative values; drafts; validation.
- Flags (low, high, critical low, critical high, abnormal option) **only** from approved reference ranges (Phase 5). No approved range means no flag, and the screen says so.
- Two-step release: technical **verification**, then **approval**. A verifier can return a result to entry with a reason.
- **Amendment** of a released result: reason required, new version, full history. The released version stays valid until the amended one is approved.
- **Critical value alerts**: raised when a submitted result crosses an approved critical limit; staff record who was told, how, and that the value was read back.
- Order page card showing result status per test (read-only).
- Audit of every result change (existing audit pipeline plus a workflow history table).

**Out of scope** (later phases): report layout, PDF and printing of reports (Phase 9), analyzer results (Phase 14), delta checks, auto-verification, bulk approval, SMS/WhatsApp to patients or doctors, interpretive comments libraries, QC/calibration.

## 2. What already exists and is reused
- Catalog: `TestParameter` (type, unit, decimal places, groups, options), `ReferenceRange` (sex, age bands in days, low/high, critical limits, Draft/Approved/Retired) and the unit-tested rules in `ReferenceRules` (`Select`, `Evaluate`). Turnaround hours per test.
- Samples (Phase 7): status **Received** at a branch, `SampleTest` (which order items a sample carries), the visibility rule `SampleService.Visible()`.
- Order items already have `InProgress` (30) and `Completed` (40) statuses; permissions `results.view|enter|verify|approve|amend|critical` and nav entries (Worklists, Result entry, Verification & approval, Critical alerts) already exist.
- Architecture: tenant filters, `[HasPermission]`, `RowVersion` concurrency (HTTP 409), idempotent sequences, `IAudited`, translation test, VISIBILITY-GATE style architecture test.

## 3. State machine

```
            save           submit              verify              approve
(none) ─► Draft ──────► Submitted ─────────► Verified ─────────► Approved (released, locked)
             ▲  discard      │ return(reason)     │ return(reason)        │ amend(reason, results.amend)
             └───────────────┴────────────────────┘                       ▼
                                                              new version: Draft … (old stays Approved
                                                              until the new one is approved → old = Superseded)
```
- Order item: Received → **InProgress** on the first saved draft → **Completed** when approved. During an amendment the item stays Completed (the released report stays valid).
- Entry needs: sample **Received** at the user's working branch, order not cancelled, item not cancelled.
- One function (`ResultRules.CanMove`) decides moves, like `SampleRules`.

## 4. Database changes (one migration, `AddResults`, schema `lab`)
| Table | Purpose / key rules |
|---|---|
| `Worklists` | `WorklistNo` (new sequence `WL`, daily), department, branch, status (Open/Closed), created by/at, notes. Unique `(OrganizationId, WorklistNo)`. |
| `WorklistItems` | `(WorklistId, OrderItemId)`. A filtered unique index allows an item on **one open** worklist only. |
| `TestResults` | One row per **version** of an order item's result: `OrderItemId`, `SampleId`, `Version`, `Status` (0 draft, 10 submitted, 20 verified, 30 approved, 90 superseded), entered/submitted/verified/approved by + time, `AmendmentReason`, `ReplacesResultId`, comment, `RowVersion`. Unique `(OrderItemId, Version)`; filtered unique index: one non-superseded version in progress per item; CHECK constraints tie status to the people/time columns (like `CK_Samples_*`). |
| `TestResultValues` | One row per parameter: value as `decimal(18,6)` **or** option id **or** text (exactly one, CHECK), **snapshot** of unit, decimal places, the range used (low/high/critical/display text, range id) and the flag. Stored values are never rounded (rounding is display only, per Phase 5). |
| `ResultApprovals` | Workflow steps: submit, verify, return, approve, amend; user, time, comment/reason. Append-only. |
| `CriticalValueAlerts` | Result value, patient, parameter, value text, limit crossed, direction, status (Open/Notified), who was notified (free text), method (phone, in person, other), read-back confirmed, notified by/at, comment. |
| Sequence `Worklist` | Added to `SequenceTypes.Defaults` (like `Transfer`). |
| Settings (no screen yet, defaults in code) | `Results.AllowSelfVerification` (false), `Results.BlockApprovalUntilCriticalNotified` (true). |

All tables: `OrganizationId`, composite FKs to order items, samples, parameters, ranges; no cascades; `[IAudited]` on mutable entities; `[Sensitive]` not needed (clinical values are visible to `results.view`, never logged in plain audit text beyond the existing audit rules). Migration seeds nothing. Existing data: none to backfill. Idempotent script and `db/MIGRATIONS.md` updated.

## 5. Backend (Application DTOs, Infrastructure services, thin controllers)
`ResultService` (entry, workflow, amend), `WorklistService`, `CriticalAlertService`; a domain `ResultRules` (state machine, separation of duties) and `ResultValidator` (value parsing). Single visibility gate `ResultService.Visible()`: a result is visible if the order's branch **or** the sample's current/origin branch is in the user's access (so collection centres can see results of their patients). Architecture test guards it, as for samples. Samples are never read here except through `SampleService`.

| Endpoint | Permission | Notes |
|---|---|---|
| `GET /api/results/pending` | results.enter | Received tests at the working branch; filters: department, priority, overdue, search/scan; overdue from `TurnaroundHours`. |
| `GET/POST /api/worklists`, `GET /{id}`, `POST /{id}/close` | results.enter | Create from selected items; sheet data for printing. |
| `GET /api/results/items/{orderItemId}` | results.enter or results.view | Entry model: patient, parameters, options, applicable approved range per parameter (sex, age at collection date), current version and values. |
| `PUT /api/results/items/{orderItemId}/draft` | results.enter | Save values, `RowVersion` concurrency. |
| `DELETE /api/results/items/{orderItemId}/draft` | results.enter | Discard a draft (needed before rejecting a sample). |
| `POST /api/results/{id}/submit` | results.enter | Validates all active parameters; computes flags; raises critical alerts. |
| `POST /api/results/{id}/return` | results.verify | Reason required; back to Draft. |
| `POST /api/results/{id}/verify` | results.verify | Not the person who entered it (unless setting). |
| `POST /api/results/{id}/approve` | results.approve | Blocked while a critical alert is open (setting); not the enterer. |
| `POST /api/results/{id}/amend` | results.amend | Reason required; creates the next version as a draft. |
| `GET /api/results/queue?stage=verification\|approval` | results.verify / results.approve | |
| `GET /api/results/{id}` and `/history` | results.view | Values, flags, workflow steps. |
| `GET /api/orders/{orderId}/results` | results.view + orders.view | Order card. |
| `GET /api/critical-alerts`, `POST /{id}/notify` | results.critical | Notify records who, how, read-back confirmed. |

Changes to existing code (small, covered by tests): `SampleService.RejectAsync` refuses a sample whose tests have saved results (`sample_has_results`; discard the draft first); the order card on `OrderDetailPage`; nav permission of "Verification & approval" becomes `results.verify`; role templates are unchanged (Technician: enter, verify, critical; Pathologist: all).

**Validation (server is authoritative):** numeric parsed with the invariant culture, finite, within `decimal(18,6)`; option must belong to the parameter and be active; text ≤ 500, narrative ≤ 4,000 characters; inactive parameters cannot be entered; every active parameter needs a value to submit; approved results can only be changed through amendment; each write checks the user's permission, the state, and `RowVersion`. Error codes get English and Urdu messages (translation test).

## 6. Frontend (English and Urdu, existing components)
1. **Worklists** (`/worklists`): pending tests table (department, priority, overdue badge, scan box), select → create worklist, saved worklists, printable A4 sheet.
2. **Result entry** (`/results/entry`, `/results/entry/:orderItemId`): patient banner, one row per parameter with the right input for its type, unit, reference range text, live flag preview (server recomputes on save), keyboard flow (Enter moves down), Save draft, Submit; a clear "no approved reference range" notice.
3. **Verification & approval** (`/results/verification`): two tabs, review panel with values, flags, critical highlight and history; Verify, Return (reason), Approve; disabled-with-explanation for the user's own entries.
4. **Critical alerts** (`/critical-alerts`): open alerts with the patient's contact details, record-notification dialog (who, method, read-back checkbox).
5. **Amend** dialog (reason) on a released result; **Results card** on the order page.
Print CSS for the worklist sheet only (reports are Phase 9). About 150 new translation keys.

## 7. Permissions
No new permission keys. Existing: `results.view` (read), `results.enter` (worklists, entry), `results.verify` (verify, return), `results.approve` (approve), `results.amend` (amend), `results.critical` (alerts). Rules on top of permissions: branch/sample visibility, separation of duties, state machine. Collection-centre reception (no `results.view`) sees only order-item statuses, as today.

## 8. Tests
**Unit:** state machine (exact allowed moves), separation-of-duties rule, value parsing and bounds (culture, rounding never applied, negatives, overflow), range selection by age at collection date, flag evaluation with draft/retired ranges ignored, architecture test for the result visibility gate, translation test (existing, picks up new codes).
**Integration (real API and database):** full path enter → submit → verify → approve with item status changes; flags only from approved ranges; critical alert raised, approval blocked until notified, then allowed; permission matrix per role; separation of duties; parallel verify/approve has one winner (409); approved result cannot be edited; amendment creates versions and supersedes correctly; option and numeric validation errors; sample not received, rejected or cancelled order refused; reject-sample guard; worklist create/close and one-open-worklist constraint; isolation between organisations and the branch visibility rule; raw-SQL constraint checks (status CHECKs, single value type); audit rows written; anonymous and CSRF-less refusal on every endpoint; regression: all existing tests.
**Browser (EN and Urdu RTL):** the whole workflow with two users, critical alert flow, amend, keyboard entry, worklist print preview.

## 9. Delivery sequence
8.1 domain, rules, migration, unit tests → 8.2 services and APIs with integration tests → 8.3 worklist screens → 8.4 result entry screen → 8.5 verification, approval, amendment → 8.6 critical alerts, order card, translations, browser verification, self-audit, docs. Each step ends with build, lint and the full test suite.

## 10. Decisions needed from you
| # | Question | Proposed default |
|---|---|---|
| 1 | Separation of duties | Nobody verifies or approves a result they entered. A pathologist holding both permissions may verify and approve another person's entry. Small labs can relax this later with a setting. |
| 2 | Two steps (verify, then approve) for every result? | Yes. The Technician verifies, the Pathologist approves. |
| 3 | Critical value alert | Raised on submit; approval is blocked until the notification (who, how, read-back) is recorded. |
| 4 | Partial results | Not allowed: all active parameters need a value to submit (an explicit "not reported" option can be added later). |
| 5 | Which reference range applies | Approved range chosen by sex and the patient's age **on the collection date**. |
| 6 | Amendment | Released result stays valid until the amended version is approved; reason mandatory; full version history. |
| 7 | Rejecting a sample after entry started | Refused until the draft is discarded; released results can never be orphaned. |
| 8 | Who can see values | Holders of `results.view` (Technician, Pathologist, Lab Manager, Administrator). Reception sees status only. |
| 9 | Bulk approval and auto-verification | Left out of this phase. |
| 10 | Overdue marker | Shown on worklists from the test's turnaround hours (counted from sample receipt). |

Clinical safety: no ranges, flags or interpretations are invented. A value without an approved range is never flagged.
