# Decisions and open questions

## Confirmed by the owner (2026-11-09)

| Topic | Decision |
|---|---|
| Location | New project at `D:\Development\New folder\LIMS` |
| Frontend | React + Vite + TypeScript |
| Dev database | Default SQL Server 2022 instance (`localhost`), database `LIMS_Dev`. Existing databases are never touched |

## Defaults in use, awaiting confirmation

| # | Question | Default used | Impact if changed |
|---|---|---|---|
| 1 | Install .NET 10 SDK (LTS)? .NET 9 support ends Nov 2026 | Target .NET 9 | Low: change target framework + bump packages |
| 2 | Do all branches reach the central server over LAN/VPN? | Yes, always connected | High: offline-sync would need a separate design |
| 3 | Server-side PDF library | Browser print-to-PDF (built in Phase 9). A server-side library is still undecided: QuestPDF would need its licence accepted | Medium |
| 4 | Currency / payment methods | PKR; cash, card, bank transfer, mobile wallet, cheque | Low |
| 5 | Password recovery | Admin-assisted reset (no email/SMS needed offline) | Low |
| 6 | Time zone | Asia/Karachi for display; UTC stored | Low |
| 7 | May the person who drafted a reference range also approve it? | Yes, if they hold `catalog.clinical`; the audit log records both. | Low: a strict two-person rule is a small change |
| 8 | Age bands in years/months | 1 year = 365.25 days, 1 month = 30.4375 days (bands stored in days) | Low |
| 9 | Who may change prices | Only roles with `catalog.pricing` (default: Lab Manager, Accountant, System Administrator) | Low: roles are configurable |
| 10 | Discount approval | Any role with `billing.discount` can give any discount, with a mandatory reason (audited). No percentage limit or second approver. | Medium: a limit (setting `Billing.MaxDiscountPercentWithoutApproval`) exists but is not enforced yet |
| 12 | Sample grouping | One sample per order per sample type (e.g. CBC + ESR share one EDTA tube); planned when the order is created | Medium: the barcode and label count follow this |
| 13 | Who sees a sample | A user sees a sample if their branch access includes its origin, current or destination branch (so a receiving branch sees what is sent to it). Everything else is hidden (404) | Medium: single gate `SampleService.Visible()`, guarded by an architecture test |
| 14 | Collection rule | The collector must tick an identity confirmation (two identifiers). Payment before collection is **not** required; the setting `Samples.RequirePaymentBeforeCollection` exists (default off) but has no screen yet | Low |
| 15 | Rejection reasons | Ten default operational reasons, editable by `catalog.manage`; the laboratory should confirm the list | Low |
| 16 | Label size | 50 × 25 mm by default, changeable per computer (stored in the browser); printed through the browser print dialog | Low: a thermal-printer driver or ZPL is a later option |
| 17 | Separation of duties | Nobody verifies or approves a result they entered. A pathologist with both permissions may verify and approve a result another person entered. Setting `Results.AllowSelfVerification` can relax this for one-person laboratories | Medium |
| 18 | Two-step release | Every result is verified, then approved; verification and approval are separate permissions | Medium |
| 19 | Critical values | Alert raised on submit; approval is blocked until the notification (who, how, read-back) is recorded (`Results.BlockApprovalUntilCriticalNotified`, default on) | Medium |
| 20 | Partial results | Not allowed: every active parameter needs a value to submit | Low |
| 21 | Reference range choice | Approved range by sex and age **on the collection date**; copied onto the result | Medium |
| 22 | Amendments | The released version stays valid until the amendment is approved; reason mandatory; versions kept | Medium |
| 23 | Rejecting a sample with results | Refused until the draft results are discarded | Low |
| 24 | Report unit and language | One report per order with versions; language (English, Urdu or both) chosen when issuing | Low |
| 25 | Interim reports | Allowed and clearly labelled; unreleased tests are listed as pending | Medium |
| 26 | Who may issue a report | `reports.issue` (Pathologist, Lab Manager, Administrator); reception and cashier print issued reports only | Medium |
| 27 | New versions | A new version supersedes the old with a mandatory reason; the old stays viewable, stamped superseded | Medium |
| 28 | Reprints | The first print is the original; later prints are marked Duplicate copy and counted | Low |
| 29 | Report integrity | SHA-256 fingerprint of the frozen snapshot, printed on the report and checkable in the system (no QR or public site, the system is offline) | Low |
| 30 | PDF | Browser print and Save as PDF. Server-side PDF (QuestPDF) is **not** included: it needs the owner to accept its licence (free below about USD 1 million yearly revenue, paid above) | Medium |
| 11 | Who may refund | Only `billing.refund` (Accountant, Lab Manager, System Administrator), and only for money due back after a cancellation. | Low |
| 31 | Where stock lives | Item definitions are organisation-wide; quantities are held per branch in batches (item + batch number + expiry). | Medium |
| 32 | When reagents are charged | When a result is **submitted**, from the item's usage profile for that test, once per ordered test (returning and re-submitting a result does not charge again). Cancelled or rejected tests that never reach a result are not charged. A stock problem never blocks or fails a result. | Medium: the laboratory should confirm the trigger (submit versus collection or approval) |
| 33 | Which batch is used | Earliest expiry first; batches without an expiry last; expired batches are never used automatically. | Low |
| 34 | Not enough stock | The result is saved and a shortfall is recorded for someone to resolve. Setting `Inventory.AllowNegativeStock` (default off, no screen yet) instead takes it into a negative "no batch" balance. | Medium |
| 35 | Ledger | Append-only; corrections are new lines; adjustments and write-offs need a reason; stock can never go below zero (also enforced by the database). | Low |
| 36 | Expiry alerts | Batches expiring within `Inventory.ExpiryAlertDays` (default 30, no screen yet) are flagged; expired batches are listed separately. | Low |
| 37 | Transfers between branches | Immediate (no in-transit state), as two linked ledger lines. The screen offers only the user's own branches; the API accepts any active branch of the organisation. | Low: an in-transit step is a later option |
| 38 | Purchases | A receipt records what arrived; there are no purchase orders, supplier payments or approval steps. | Low |
| 39 | Dashboard scope | Figures follow the working branch (all visible branches when none is chosen); total patients is always organisation-wide. Each part is shown only to people who hold its permission. | Low |
| 40 | "Overdue" sample | Planned or collected samples older than 24 hours (fixed rule, no setting). | Medium: the laboratory should confirm the number of hours |
| 41 | "Payments received" | Counter payments only (not corporate allocations, which would count the same money twice) and not net of refunds. Corporate "credit billed" is the net of credit invoices created that day. | Medium: finance may prefer net of refunds |
| 42 | Dashboard refresh | Reads live data on every request; the open page refreshes every minute. Nothing is cached or stored. | Low |
| 43 | Who may read the audit log | Anyone with `audit.view` (System Administrator and Lab Manager by default) sees the whole organisation's log, not only their own branches. | Medium: a branch-limited audit role is a later option |
| 44 | Audit log is read-only | No endpoint creates, changes or deletes a line; sensitive values are masked when written. Viewing the log is not itself logged. | Low |
| 45 | Which settings are editable | Only the five settings the code reads today. `Billing.MaxDiscountPercentWithoutApproval` stays hidden until a discount limit is actually enforced. Settings are organisation-wide. | Low |

## Clinical safety rules (fixed)

- No reference ranges, critical limits or interpretation rules are seeded. They must be entered and approved by qualified laboratory staff.
- High/low flags appear only when an approved range exists for that parameter, sex and age.
