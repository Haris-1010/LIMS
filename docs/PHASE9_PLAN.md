# Phase 9 plan: bilingual report templates, PDF and printing

_Status: **approved and built** (see `IMPLEMENTATION_STATUS.md`). Decision 1 was approved with its default: browser print and Save as PDF; the optional server-side PDF was not built because the QuestPDF licence has not been accepted._

## 1. Scope

**In scope**
- A patient report per **order**, built only from **approved (released) results**, in English, Urdu or both, on A4.
- Letterhead (organisation and branch details, logo), patient and sample details, results grouped by department with units, **approved** reference ranges and flags, comments, verifier and approver names with designation, registration number and optional signature image, report number, version, page numbers and an integrity fingerprint.
- **Preview before finalising** (watermarked, not stored), then **Issue**: the report is frozen as an immutable snapshot.
- Versions: a later amendment or a newly released test produces a new version; the old one stays viewable and is stamped SUPERSEDED.
- Printing from the browser (A4 print stylesheet, local Urdu font) and **PDF export** (see decision 1).
- Secure access: permissions, branch visibility, and an audit record of every view, print and download.
- Editable report template per organisation (header and footer text, disclaimer, logo, in English and Urdu).

**Out of scope** (later phases or not requested): email, SMS or WhatsApp delivery, patient portal, QR or public verification (the system is offline on a LAN; the fingerprint is checked inside the system), corporate report packs (Phase 10), a drag-and-drop template designer, interpretive comments or diagnostic text, analyzer-specific layouts (Phase 14).

## 2. What already exists and is reused
- Released results with their **copied** unit, decimals, range and flag (`TestResultValues`); verifier and approver ids and times (`TestResults`); test report notes (`LabTest.ReportNotes`); parameter groups and sort order.
- Organisation and branch names, addresses and phones in English and Urdu; user designation; local fonts Inter and **Noto Nastaliq Urdu**; permissions `reports.view` and `reports.print` (already in the role templates); the audit pipeline and `AddBusinessAudit`; the `ResultService.Visible()`-style visibility rule; the print patterns used for receipts, labels and sheets.

## 3. Key design points
- **Snapshot, not live data.** Issuing stores a JSON snapshot of exactly what is printed (patient, order, sample dates, every value with its flag and range, signatories, template text) plus its **SHA-256 fingerprint**. A report can never change silently when catalogue data, a patient name or a range changes later. Verifying a report recomputes the fingerprint.
- **Values are never reformatted clinically.** Display rounding uses the parameter's decimal places only; the stored number is what is printed. A test that is not yet released is listed as "Pending", never guessed.
- **Interim and final.** A report with every non-cancelled test released is *Final*; otherwise *Interim* (clearly labelled). New releases or amendments make the issued version "outdated" on the Reports screen and require issuing a new version.
- **One renderer.** A single React report component renders preview, print and (if chosen) PDF from the snapshot, so what staff preview is what prints.
- **Language.** Chosen at issue: English, Urdu, or bilingual (names in both languages, values and units once). Urdu uses RTL layout, local Noto Nastaliq font, correct number alignment (digits stay left-to-right).

## 4. Database changes (one migration, `AddReports`)
| Table / change | Purpose and rules |
|---|---|
| `lab.ReportTemplates`, `lab.ReportTemplateTranslations` | One template per organisation (branch override left for later). Translations per language (`en`, `ur`): header note, footer note, disclaimer. Logo as `varbinary(max)` (PNG or JPEG, ≤ 300 KB, content type checked by signature bytes). |
| `lab.LabReports` | `OrderId`, `ReportNo` (new sequence `Report`, monthly) and `Version`, status (Interim, Final, Superseded), language, `IssuedBy/At`, `AmendmentReason`, `ReplacesReportId`, `SnapshotJson`, `SnapshotHash` (char 64), `PrintCount`, `LastPrintedAtUtc`, order and lab branch ids for visibility. Unique `(OrderId, Version)`; filtered unique index: one non-superseded version per order; CHECKs tie status, issuer and hash. Immutable after issue except status → Superseded and the print counters. |
| `security.Users` | Add `RegistrationNo` (e.g. medical council number), `SignatureImage` (`varbinary(max)`, ≤ 200 KB), `SignatureContentType`. |
| Permissions | New rows `reports.issue` (finalise a report) and `reports.templates` (edit the template) appended to the catalogue; the migration grants them to the existing System Administrator, Lab Manager and (issue only) Pathologist roles of every organisation, and provisioning does the same for new ones. No existing key is renumbered. |
| Sequence | `Report` (`REP`, monthly), created on first use like `Worklist`. |
No data to backfill. Idempotent script and `db/MIGRATIONS.md` updated.

## 5. Backend (Application DTOs, Infrastructure services, thin controllers)
`ReportService` (ready list, preview, issue, versions, snapshot read, integrity check, access recording), `ReportTemplateService`, a domain `ReportRules` (interim/final, outdated detection, copy marking) and `ReportSnapshotBuilder` (the only place that turns results into report content). Visibility: a report is visible when its order branch or lab branch is in the user's access (single gate, architecture-tested like samples and results). Released results are read through `ResultService`-owned queries, never around its gate.

| Endpoint | Permission | Notes |
|---|---|---|
| `GET /api/reports/ready` | reports.view | Orders with released results and no current report, or an outdated one; paging, search, branch. |
| `GET /api/orders/{id}/reports` | reports.view | Versions of the order's report. |
| `POST /api/orders/{id}/reports/preview` | reports.view | Builds a snapshot on the fly (not stored). |
| `POST /api/orders/{id}/reports` | reports.issue | Language, reason (required for version 2+). Freezes the snapshot. |
| `GET /api/reports/{id}` | reports.view | Snapshot and metadata; records the view. |
| `POST /api/reports/{id}/printed` | reports.print | Counts the print; later prints are marked as copies. |
| `GET /api/reports/{id}/pdf` | reports.print | Only if server-side PDF is approved (decision 1). |
| `GET /api/reports/{id}/verify` | reports.view | Recomputes and compares the fingerprint. |
| `GET/PUT /api/report-template`, `PUT/DELETE …/logo` | reports.templates (read: reports.view) | Text per language and the logo. |
| `PUT /api/users/{id}/signature`, `DELETE` | users.manage | Registration number and signature image. |

**Validation:** only released results are included; an order with none cannot be reported; language must be known; the reason is mandatory for amendments; images are size-limited and type-checked by their bytes (not file name); template text lengths limited; snapshot size capped. Every endpoint is permission-gated, CSRF-protected and tenant/branch isolated. New error codes get English and Urdu messages (translation test).

## 6. Frontend (English and Urdu, existing components)
1. **Reports** (`/reports`): "Ready to report" and "Issued" tabs with search by order, patient or report number; outdated and interim badges.
2. **Order report panel / page** (`/reports/order/:orderId`): versions list; Preview, Issue (language, reason), Print, Download PDF, Verify integrity; copy and superseded markers.
3. **Report document** component: A4 layout, letterhead, patient block, grouped results with flags (letter codes **and** words so black-and-white prints stay clear), comments, signatories with signature images, footer with report number, version, page number and fingerprint; English, Urdu RTL and bilingual variants; print stylesheet with repeated table headers and no clipped Nastaliq glyphs.
4. **Template settings** (`/reports/template`): header, footer, disclaimer per language, logo upload with preview.
5. **Users**: registration number and signature upload in the existing user form.
6. Order page: a "Report" link and status. About 120 new translation keys; nav: Reports (existing) plus Template under Configuration.

## 7. Permissions
`reports.view` (list, preview, read, verify), `reports.print` (print and download), **new** `reports.issue`, **new** `reports.templates`. Defaults: Pathologist, Lab Manager, Administrator can issue; Reception and Cashier can print issued reports only; Technician can preview. Collection-centre staff see reports of their branch's orders through the same visibility rule as results.

## 8. Tests
**Unit:** interim/final and outdated rules; snapshot builder (only released values, rounding for display only, pending tests, bilingual names, flag codes); fingerprint determinism and tamper detection; copy marking; image signature sniffing (PNG/JPEG accepted, renamed `.exe`/SVG/GIF refused); report architecture test for the visibility gate.
**Integration (real API and database):** preview does not store; issue freezes values and a later range or name change does not alter it; amendment of a result makes the report outdated and a new version supersedes the old; reason required for v2; interim then final; unreleased tests never appear; permissions per role (reception prints but cannot issue; technician previews only); isolation between organisations and branches; double-issue race has one winner; print counting and copy flag; fingerprint verify fails after a raw-SQL tamper; image upload limits; template read/write; access recorded in audit; raw-SQL constraints (one live version per order, status/hash checks); anonymous and CSRF-less refusal; regression of all existing tests.
**Browser (EN and Urdu RTL):** preview, issue, print preview (print media emulation), multi-page report with repeating header, Urdu digits and alignment, bilingual layout, signature and logo, supersede flow, template editing. PDF checked by opening the generated file and extracting its text, if server-side PDF is built.

## 9. Delivery sequence
9.1 PDF feasibility spike (Urdu shaping with the local Nastaliq font) and decision, domain rules, migration, unit tests → 9.2 services and APIs with integration tests → 9.3 report document component, print stylesheet, template settings → 9.4 Reports screens (ready list, order panel, preview, issue, versions) → 9.5 signatures on users, permissions, access audit, order-page link → 9.6 Urdu/bilingual print verification, self-audit, docs. Each step ends with build, lint and the full test suite.

## 10. Decisions needed from you
| # | Question | Proposed default |
|---|---|---|
| 1 | **PDF export** | Ship browser print and "Save as PDF" first (no new dependency, correct Urdu, works offline). Add one-click server-side PDF with **QuestPDF** only if the spike shows correct Urdu **and** you accept its licence (free for organisations under about USD 1 million yearly revenue, paid above that). If not, Phase 9 ships browser print only. |
| 2 | Report unit | One report per **order**, with versions. |
| 3 | Interim reports | Allowed and clearly labelled; they list unreleased tests as "Pending". |
| 4 | Language | Chosen at issue: English, Urdu or bilingual; organisation default English. |
| 5 | Who may issue | `reports.issue` (Pathologist, Lab Manager, Administrator). Reception prints issued reports only. |
| 6 | Amendments | A new version supersedes the old, with a mandatory reason; the old one stays viewable, stamped SUPERSEDED. |
| 7 | Reprints | The first print is the original; every later print is marked "Duplicate copy" and counted. |
| 8 | Signatories | Every distinct approver of the included results: name, designation, registration number, optional signature image. |
| 9 | Integrity | SHA-256 fingerprint of the frozen snapshot printed on the report and checkable in the system (no QR or public site, as the system is offline). |
| 10 | Logo and images | PNG or JPEG only, stored in the database (logo ≤ 300 KB, signature ≤ 200 KB). |
| 11 | Delivery to patients | Out of scope (no email, SMS or WhatsApp); printing and PDF only. |

Clinical safety: no values, ranges or interpretations are invented or reworded; the report shows exactly what was released, with the range that was used when it was released.
