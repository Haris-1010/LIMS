# Phase 11: inventory and stock ledger

_Status: **built and tested** (see `IMPLEMENTATION_STATUS.md`). The decisions below (31-38 in `DECISIONS.md`) were taken as sensible defaults so the phase could proceed; they are open for the owner to change._

## 1. Scope
- **Stock items** (reagents and consumables): code, English/Urdu name, unit ("kit", "ml"), reorder level, whether an expiry date is required, usual supplier. Definitions are organisation-wide; **quantities are per branch**, in batches.
- **Suppliers**: code, names, contact details.
- **Purchases ("receive stock")**: one receipt per delivery with any number of lines (item, batch number, expiry date, quantity, unit cost). Numbered `GRN-...`. A form carries a one-time key, so a double click or retry records it once.
- **Batches**: stock of one item received together (same batch number and expiry) at one branch. Receiving the same batch again adds to it, but only with the same expiry date.
- **Stock ledger**: every change to stock is one append-only line with the balance after it. Mistakes are corrected by a new line, never an edit.
- **Manual changes** on a batch: issue (use), adjust in, adjust out, write off. Adjustments and write-offs need a reason.
- **Transfers between branches**: out of the source batch and into a matching batch at the destination (the expiry travels with it), as two linked ledger lines with an `STR-...` reference.
- **Automatic consumption**: each test can list the items it uses and how much per run. When a result for that test is **submitted**, the amount is taken from stock, **earliest expiry first**; expired batches are never used automatically. Charged once per ordered test, however often the result is returned and submitted again.
- **Never blocks a result.** If stock cannot cover a test, the result is saved anyway and the missing amount is recorded as a **shortfall** for someone to resolve (or, when the setting `Inventory.AllowNegativeStock` is on, taken into a negative "no batch" balance).
- **Alerts**: low stock (total on hand at or below the reorder level), batches expiring within `Inventory.ExpiryAlertDays` (default 30) and expired batches, and open shortfalls.

**Out of scope** (later phases or not requested): purchase orders and approval, supplier invoices/payments (accounts), stock counts (stocktake) screens, barcode scanning of packs, per-test consumption reports, automatic reordering, cost-of-goods reporting, an "in transit" state for stock transfers, settings screens for the two inventory settings (Phase 12).

## 2. Permissions
`inventory.view` (items, ledger, alerts), `inventory.manage` (items, suppliers, usage profile), `inventory.purchase` (receive stock), `inventory.adjust` (issue, adjust, write off, transfer, resolve shortfalls). Already in the permission catalogue. The Inventory Manager holds all four, the Technician can view, and the Lab Manager and System Administrator hold every permission.

## 3. Database (migration `AddInventory`)
Schema `inventory`: `Suppliers`, `StockItems`, `StockBatches`, `StockMovements`, `PurchaseReceipts`, `PurchaseLines`, `ConsumptionProfileLines`, `StockShortfalls`.
- Composite organisation foreign keys; unique item code, supplier code, receipt number and purchase submission key; one batch per item, branch and batch number.
- CHECK constraints: stock on hand is never negative (except the `UNBATCHED` catch-all), a ledger line is never zero and its direction matches its type, quantities and costs are positive.
- One automatic charge per test run, item and batch (unique `ConsumptionKey`).
- Every change to a batch is one conditional `UPDATE ... OUTPUT`, so two people can never take the same stock and stock never goes below zero.
- New sequences `PurchaseReceipt` (`GRN`, yearly) and `StockTransfer` (`STR`, monthly), created on first use.

## 4. API (`/api/inventory/...`)
`GET|POST items`, `GET|PUT items/{id}`, `PUT items/{id}/profile`, `GET|POST suppliers`, `PUT suppliers/{id}`, `GET|POST purchases`, `GET purchases/{id}`, `POST movements`, `POST transfers`, `GET ledger`, `GET alerts`, `GET shortfalls`, `POST shortfalls/{id}/resolve`.

## 5. Screens
Stock items (alerts strip, low-only filter, expiry badges), stock item page (batches per branch with actions, usage by tests), Suppliers, Purchases (receive stock dialog, receipt view), Stock ledger and Shortfalls. English and Urdu RTL.
