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
| 3 | Server-side PDF library | Browser print-to-PDF; decide on a server-side PDF library in Phase 9 | Medium |
| 4 | Currency / payment methods | PKR; cash, card, bank transfer, mobile wallet, cheque | Low |
| 5 | Password recovery | Admin-assisted reset (no email/SMS needed offline) | Low |
| 6 | Time zone | Asia/Karachi for display; UTC stored | Low |
| 7 | May the person who drafted a reference range also approve it? | Yes, if they hold `catalog.clinical`; the audit log records both. | Low: a strict two-person rule is a small change |
| 8 | Age bands in years/months | 1 year = 365.25 days, 1 month = 30.4375 days (bands stored in days) | Low |
| 9 | Who may change prices | Only roles with `catalog.pricing` (default: Lab Manager, Accountant, System Administrator) | Low: roles are configurable |
| 10 | Discount approval | Any role with `billing.discount` can give any discount, with a mandatory reason (audited). No percentage limit or second approver. | Medium: a limit (setting `Billing.MaxDiscountPercentWithoutApproval`) exists but is not enforced yet |
| 11 | Who may refund | Only `billing.refund` (Accountant, Lab Manager, System Administrator), and only for money due back after a cancellation. | Low |

## Clinical safety rules (fixed)

- No reference ranges, critical limits or interpretation rules are seeded. They must be entered and approved by qualified laboratory staff.
- High/low flags appear only when an approved range exists for that parameter, sex and age.
