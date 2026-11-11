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
| 17 | Separation of duties | Nobody verifies or approves a result they entered. A pathologist with both permissions may verify and approve a result another person entered. Setting `Results.AllowSelfVerification` relaxes this for one-person laboratories; **default on since 2026-11-12 (owner)**, a laboratory with separate reviewers switches it off | Medium |
| 18 | Two-step release | Every result is verified, then approved; verification and approval are separate permissions | Medium |
| 19 | Critical values | Alert raised on submit; approval is blocked until the notification (who, how, read-back) is recorded (`Results.BlockApprovalUntilCriticalNotified`); **default off since 2026-11-12 (owner)**, so a one-person laboratory is not blocked; the alert is still raised | Medium |
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

| 46 | HTTPS on the LAN | Required: redirect, HSTS and Secure cookie are built and on in the production template. Needs a certificate from the lab's authority or self-signed and installed on every PC. | High until a real certificate is installed |
| 47 | Hosting | IIS in-process on Windows Server with the .NET 9 Hosting Bundle (`DEPLOYMENT.md`). | Low |
| 48 | Production SQL Server edition and version | **Not known.** Express has no SQL Agent (Task Scheduler is used), no backup compression and a 10 GB database limit. | Medium: needed before the volume and backup choices are final |
| 49 | Recovery limits | Lose at most 15 minutes of work (log backups every 15 minutes); back running within 2 hours (to be measured in the restore drill). | Medium: owner to confirm the limits |
| 50 | Backup destination, encryption, who is told of a failure | A second disk plus a second machine or removable drive; encrypted with a certificate; failures go to the Windows event log and `backup-status.json` and can run a notification script. **Who is told and how is not decided.** | Medium |
| 51 | Expected volume | Assumed 300 orders a day, 3 branches, 30 users at once, 5 years (about 550,000 orders). To correct. | Medium: sets the load-test size and the targets |
| 52 | Audit log retention | Keep everything for now; revisit when the table is large. | Low |
| 53 | Independent penetration test | Not part of Phase 13; recommended before go-live if the system is reachable from outside the lab network. | Medium |
| 54 | Sign-in lockout (SECURITY_REVIEW O1) | Kept as is: 5 wrong passwords lock the account 15 minutes; this reveals that a username exists. | Low-Medium: owner to accept or change |
| 55 | Password rule (O2) | Kept as is: 8 characters, a letter and a digit. A longer minimum is recommended. | Low: owner to decide |
| 56 | "Remember me" (O3) | Kept as is: 7 days. | Low: owner to decide, especially for shared PCs |
| 57 | Public health endpoint (O4) | Kept open: the deploy script and monitoring use it. | Low |
| 58 | Sending reports on WhatsApp (owner, 2026-11-11) | A WhatsApp link (wa.me) opened by the browser with the number and a message; staff save the PDF and attach it in WhatsApp. No WhatsApp Business API: the server stays offline and no patient data goes to a third party. The PC that shares needs internet. Only the live version can be shared; the number is checked by the API and kept out of the audit log. | Medium: needs internet on the sharing PC |
| 59 | Report designs (owner, 2026-11-11) | Ready-made styles with settings (colours, text size, margins, letterhead on/off, logo position, table style, range bars, order of the parts), not a free drag-and-drop canvas. A design is chosen when viewing or printing and is not frozen into the snapshot: it changes the look only, and every part (results, pending tests, signatories, report number, fingerprint) always prints. | Low |
| 60 | Partial prints (owner, 2026-11-11) | Within one report only. The copy says "shows X of Y tests"; the audit line records which tests. The issued report itself is unchanged. | Low |
| 61 | Starter test catalog (owner, 2026-11-12) | 29 commonly ordered tests (CBC, LFT, RFT, lipids, thyroid, serology, urine/stool R/E…) with parameters, options and typical adult reference ranges and some critical limits are added to each organisation by `db migrate` and `db create-org` (once per organisation; `--no-starter-catalog` skips). Every range is a **draft** until approved in the range approval screen; prices start at 0; children get no range until the laboratory adds one. Tenant data cannot live in an EF migration (organisations do not exist yet), so it is added by the server command right after migrating. | Medium: the laboratory must check every range against its own analyser and kit insert before approving |
| 62 | One test per page (owner, 2026-11-12) | Every test of a report prints on its own A4 page, each page with the full branding (letterhead or header image, patient details, signatures, disclaimer, footer, report number and fingerprint, "Page n of N"). A test never runs onto a second page: a page whose content is too tall is scaled down evenly until it fits. Tests still awaited are listed on the last page. Layout only: the snapshot and its fingerprint are unchanged. Replaces "the whole report on one page". | Low: a very long test is printed small |

## Clinical safety rules (fixed)

- No reference range or critical limit is ever **approved** automatically. The starter catalog (decision 61) only proposes drafts; they flag nothing until qualified laboratory staff approve them.
- High/low flags appear only when an approved range exists for that parameter, sex and age.
