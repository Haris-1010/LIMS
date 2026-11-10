# Phase 10: corporate billing and reconciliation

_Status: **built and tested** (see `IMPLEMENTATION_STATUS.md`). Written after the build, from the code in the repository._

## 1. Scope
- **Corporate clients**: code, English/Urdu name, contact, credit limit (optional), payment terms in days.
- **Contracts** per client: validity dates, a percentage discount and optional fixed price per test. Two active contracts of one client may not cover the same day.
- **Credit orders**: an order for a corporate client is priced from the contract (fixed price, else catalogue price, then the contract discount) and raises an ordinary invoice that carries the client and a due date. No counter payment and no manual discount on a credit order. A credit limit stops an order that would take the balance past it.
- **Receipts and credit notes**: money received from a client (card, bank transfer, wallet, cheque; not cash) or an authorised credit note (reason mandatory). Each has a one-time submission key.
- **Allocation**: a receipt is matched to invoices; each match is recorded as a `Corporate` payment on the invoice, so invoice arithmetic stays the one billing already uses. Never beyond what the receipt has left or the invoice owes.
- **Account**: balance (invoiced less received), unpaid invoices, unallocated receipts, credit available, **aging** (not due, 1-30, 31-60, 61-90, over 90 days past due).
- **Statement** for a date range with opening balance and running balance.
- **Reconcile ("Check books")**: receipts against allocations, allocations against payments, payments against invoices, balance against totals.

**Out of scope**: sending statements by email or SMS, a printable statement/PDF layout, interest or late fees, one grouped invoice per month (credit invoices stay one per order), per-branch client accounts, a corporate report pack.

## 2. Permissions
`corporate.view`, `corporate.manage` (clients, contracts), `corporate.invoice` (raise credit orders), `corporate.receive` (receipts, allocation), `corporate.adjust` (credit notes).

## 3. Database (migration `AddCorporateBilling`)
`billing.CorporateClients`, `CorporateContracts`, `CorporateContractPrices`, `CorporateReceipts`, `CorporateAllocations`; `Orders` and `Invoices` gain `CorporateClientId` and `ContractId`, `Invoices` also `DueDate`; the payment-method CHECK is replaced to allow `Corporate`; the existing system Corporate Billing Officer role of each organisation is granted `orders.create` and `catalog.view`. Receipts use a new sequence (`CRC`, monthly), created on first use. Composite organisation foreign keys, unique receipt number and submission key, CHECK constraints on amounts.

## 4. API (`/api/corporate/...`)
`GET|POST clients`, `GET|PUT clients/{id}`, `POST clients/{id}/contracts`, `PUT clients/contracts/{id}`, `GET clients/{id}/account`, `GET clients/{id}/statement`, `GET invoices`, `GET receipts[/{id}]`, `POST receipts`, `POST receipts/{id}/allocate`, `GET reconcile`.

## 5. Screens
Corporate clients list, client account page (balance, aging, contracts, credit invoices, receipts, statement, check books, new receipt, allocation), credit invoices list; client and payment-method choice on the new-order page.
