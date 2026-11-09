namespace Lims.Domain.Security;

public sealed record PermissionDefinition(int Id, string Key, string Module, string DescriptionEn, string DescriptionUr);

/// <summary>
/// Every permission the system checks. IDs are stable and seeded by migration: never renumber or
/// reuse an ID; add new ones at the end. The API authorises by these keys, never by role names.
/// </summary>
public static class Permissions
{
    public const string DashboardView = "dashboard.view";

    public const string PatientsView = "patients.view";
    public const string PatientsCreate = "patients.create";
    public const string PatientsEdit = "patients.edit";
    public const string PatientsViewSensitive = "patients.viewSensitive";
    public const string PatientsMerge = "patients.merge";
    public const string DoctorsView = "doctors.view";
    public const string DoctorsManage = "doctors.manage";

    public const string OrdersView = "orders.view";
    public const string OrdersCreate = "orders.create";
    public const string OrdersCancel = "orders.cancel";

    public const string BillingView = "billing.view";
    public const string BillingDiscount = "billing.discount";
    public const string BillingRefund = "billing.refund";
    public const string PaymentsCollect = "payments.collect";
    public const string CashClose = "cash.close";

    public const string CorporateView = "corporate.view";
    public const string CorporateManage = "corporate.manage";
    public const string CorporateInvoice = "corporate.invoice";
    public const string CorporateReceive = "corporate.receive";
    public const string CorporateAdjust = "corporate.adjust";

    public const string SamplesView = "samples.view";
    public const string SamplesCollect = "samples.collect";
    public const string SamplesReceive = "samples.receive";
    public const string SamplesReject = "samples.reject";
    public const string SamplesTransfer = "samples.transfer";

    public const string ResultsView = "results.view";
    public const string ResultsEnter = "results.enter";
    public const string ResultsVerify = "results.verify";
    public const string ResultsApprove = "results.approve";
    public const string ResultsAmend = "results.amend";
    public const string ResultsCritical = "results.critical";

    public const string ReportsView = "reports.view";
    public const string ReportsPrint = "reports.print";

    public const string CatalogView = "catalog.view";
    public const string CatalogManage = "catalog.manage";
    public const string CatalogClinical = "catalog.clinical";
    public const string CatalogPricing = "catalog.pricing";

    public const string InventoryView = "inventory.view";
    public const string InventoryManage = "inventory.manage";
    public const string InventoryPurchase = "inventory.purchase";
    public const string InventoryAdjust = "inventory.adjust";

    public const string OrgManage = "org.manage";
    public const string UsersManage = "users.manage";
    public const string RolesManage = "roles.manage";
    public const string AuditView = "audit.view";
    public const string SettingsManage = "settings.manage";
    public const string BackupManage = "backup.manage";

    public static readonly IReadOnlyList<PermissionDefinition> All =
    [
        new(1, DashboardView, "Dashboard", "View dashboard and statistics", "ڈیش بورڈ اور اعداد و شمار دیکھیں"),

        new(2, PatientsView, "Patients", "Search and view patients", "مریض تلاش کریں اور دیکھیں"),
        new(3, PatientsCreate, "Patients", "Register patients", "مریضوں کا اندراج"),
        new(4, PatientsEdit, "Patients", "Edit patient details", "مریض کی تفصیلات میں ترمیم"),
        new(5, PatientsViewSensitive, "Patients", "View national ID and full contact details", "شناختی کارڈ اور مکمل رابطہ تفصیلات دیکھیں"),
        new(6, PatientsMerge, "Patients", "Merge duplicate patient records", "دہرے ریکارڈ یکجا کریں"),
        new(7, DoctorsView, "Patients", "View doctors", "ڈاکٹرز دیکھیں"),
        new(8, DoctorsManage, "Patients", "Manage doctors and referrers", "ڈاکٹرز کا انتظام"),

        new(9, OrdersView, "Orders", "View lab orders", "لیب آرڈرز دیکھیں"),
        new(10, OrdersCreate, "Orders", "Create lab orders", "لیب آرڈر بنائیں"),
        new(11, OrdersCancel, "Orders", "Cancel orders or tests", "آرڈر یا ٹیسٹ منسوخ کریں"),

        new(12, BillingView, "Billing", "View invoices", "انوائسز دیکھیں"),
        new(13, BillingDiscount, "Billing", "Apply discounts", "رعایت دیں"),
        new(14, BillingRefund, "Billing", "Issue refunds and reversals", "رقم کی واپسی اور منسوخی"),
        new(15, PaymentsCollect, "Billing", "Collect payments and print receipts", "ادائیگی وصول کریں اور رسید پرنٹ کریں"),
        new(16, CashClose, "Billing", "Close cash drawer and day", "کیش دراز اور دن بند کریں"),

        new(17, CorporateView, "Corporate", "View corporate clients and balances", "کارپوریٹ کلائنٹس اور بیلنس دیکھیں"),
        new(18, CorporateManage, "Corporate", "Manage clients, contracts and price lists", "کلائنٹس، معاہدوں اور قیمتوں کا انتظام"),
        new(19, CorporateInvoice, "Corporate", "Generate corporate credit invoices", "کارپوریٹ ادھار انوائس بنائیں"),
        new(20, CorporateReceive, "Corporate", "Record corporate payments and allocations", "کارپوریٹ ادائیگیاں درج کریں"),
        new(21, CorporateAdjust, "Corporate", "Authorise credit adjustments", "ادھار میں ترمیم کی منظوری"),

        new(22, SamplesView, "Samples", "View and track samples", "نمونے دیکھیں اور ٹریک کریں"),
        new(23, SamplesCollect, "Samples", "Collect samples and print barcodes", "نمونے لیں اور بارکوڈ پرنٹ کریں"),
        new(24, SamplesReceive, "Samples", "Receive samples in the laboratory", "لیبارٹری میں نمونے وصول کریں"),
        new(25, SamplesReject, "Samples", "Reject samples and request recollection", "نمونہ مسترد کریں اور دوبارہ طلب کریں"),
        new(26, SamplesTransfer, "Samples", "Transfer samples between branches", "برانچز کے درمیان نمونہ منتقل کریں"),

        new(27, ResultsView, "Results", "View results", "نتائج دیکھیں"),
        new(28, ResultsEnter, "Results", "Enter and save results", "نتائج درج کریں"),
        new(29, ResultsVerify, "Results", "Technically verify results", "نتائج کی تکنیکی تصدیق"),
        new(30, ResultsApprove, "Results", "Approve and release results", "نتائج کی منظوری اور اجرا"),
        new(31, ResultsAmend, "Results", "Amend approved results (with reason)", "منظور شدہ نتائج میں ترمیم (وجہ کے ساتھ)"),
        new(32, ResultsCritical, "Results", "Handle critical value alerts", "تشویشناک نتائج کے الرٹس نمٹائیں"),

        new(33, ReportsView, "Reports", "View laboratory reports", "لیبارٹری رپورٹس دیکھیں"),
        new(34, ReportsPrint, "Reports", "Print and export reports", "رپورٹس پرنٹ اور ایکسپورٹ کریں"),

        new(35, CatalogView, "Catalog", "View test catalog", "ٹیسٹ کیٹلاگ دیکھیں"),
        new(36, CatalogManage, "Catalog", "Manage tests, parameters and packages", "ٹیسٹ، پیرامیٹرز اور پیکجز کا انتظام"),
        new(37, CatalogClinical, "Catalog", "Approve reference ranges and critical limits", "ریفرنس رینجز اور تشویشناک حدود کی منظوری"),
        new(38, CatalogPricing, "Catalog", "Manage test prices", "ٹیسٹ کی قیمتوں کا انتظام"),

        new(39, InventoryView, "Inventory", "View stock and ledger", "اسٹاک اور لیجر دیکھیں"),
        new(40, InventoryManage, "Inventory", "Manage items and suppliers", "اشیاء اور سپلائرز کا انتظام"),
        new(41, InventoryPurchase, "Inventory", "Record purchases and stock receipts", "خریداری اور اسٹاک کی وصولی درج کریں"),
        new(42, InventoryAdjust, "Inventory", "Adjust and transfer stock", "اسٹاک میں ترمیم اور منتقلی"),

        new(43, OrgManage, "Administration", "Manage organisation, branches and departments", "ادارہ، برانچز اور شعبہ جات کا انتظام"),
        new(44, UsersManage, "Administration", "Manage users", "صارفین کا انتظام"),
        new(45, RolesManage, "Administration", "Manage roles and permissions", "کردار اور اجازتوں کا انتظام"),
        new(46, AuditView, "Administration", "View audit logs", "آڈٹ لاگز دیکھیں"),
        new(47, SettingsManage, "Administration", "Change system settings", "سسٹم سیٹنگز تبدیل کریں"),
        new(48, BackupManage, "Administration", "Run and restore backups", "بیک اپ اور بحالی"),
    ];
}

public sealed record RoleTemplate(string Code, string NameEn, string NameUr, string Description, IReadOnlyList<string> Permissions);

/// <summary>
/// Starting roles created for a new organisation. They are ordinary, editable roles afterwards;
/// administrators can change their permissions or add new roles.
/// </summary>
public static class DefaultRoles
{
    private static readonly string[] AllKeys = Permissions.All.Select(p => p.Key).ToArray();

    public static readonly IReadOnlyList<RoleTemplate> All =
    [
        new("SYSADMIN", "System Administrator", "سسٹم ایڈمنسٹریٹر", "Full access to every module", AllKeys),
        new("LABMGR", "Lab Manager", "لیب مینیجر", "Oversees laboratory operations, configuration and staff",
            AllKeys.Except([Permissions.BackupManage]).ToArray()),
        new("RECEPTION", "Receptionist", "ریسپشنسٹ", "Registers patients and creates orders",
        [
            Permissions.DashboardView, Permissions.PatientsView, Permissions.PatientsCreate, Permissions.PatientsEdit,
            Permissions.DoctorsView, Permissions.OrdersView, Permissions.OrdersCreate, Permissions.BillingView,
            Permissions.PaymentsCollect, Permissions.SamplesView, Permissions.ReportsView, Permissions.ReportsPrint,
            Permissions.CatalogView,
        ]),
        new("CASHIER", "Cashier", "کیشیئر", "Collects payments and closes the cash drawer",
        [
            Permissions.DashboardView, Permissions.PatientsView, Permissions.OrdersView, Permissions.BillingView,
            Permissions.PaymentsCollect, Permissions.CashClose, Permissions.ReportsView, Permissions.ReportsPrint,
        ]),
        new("PHLEBOTOMY", "Phlebotomist", "فلیبوٹومسٹ", "Collects samples and prints barcodes",
        [
            Permissions.PatientsView, Permissions.OrdersView, Permissions.SamplesView, Permissions.SamplesCollect,
            Permissions.SamplesReject,
        ]),
        new("TECHNICIAN", "Lab Technician", "لیب ٹیکنیشن", "Receives samples, enters and verifies results",
        [
            Permissions.DashboardView, Permissions.PatientsView, Permissions.OrdersView, Permissions.SamplesView,
            Permissions.SamplesReceive, Permissions.SamplesReject, Permissions.SamplesTransfer, Permissions.ResultsView,
            Permissions.ResultsEnter, Permissions.ResultsVerify, Permissions.ResultsCritical, Permissions.ReportsView,
            Permissions.CatalogView, Permissions.InventoryView,
        ]),
        new("PATHOLOGIST", "Pathologist", "پیتھالوجسٹ", "Approves results, amends reports and approves clinical configuration",
        [
            Permissions.DashboardView, Permissions.PatientsView, Permissions.PatientsViewSensitive, Permissions.OrdersView,
            Permissions.SamplesView, Permissions.SamplesReject, Permissions.ResultsView, Permissions.ResultsEnter,
            Permissions.ResultsVerify, Permissions.ResultsApprove, Permissions.ResultsAmend, Permissions.ResultsCritical,
            Permissions.ReportsView, Permissions.ReportsPrint, Permissions.CatalogView, Permissions.CatalogClinical,
        ]),
        new("ACCOUNTANT", "Accountant", "اکاؤنٹنٹ", "Financial reporting, refunds and reconciliation",
        [
            Permissions.DashboardView, Permissions.OrdersView, Permissions.BillingView, Permissions.BillingRefund,
            Permissions.CashClose, Permissions.CorporateView, Permissions.CorporateReceive, Permissions.CorporateAdjust,
            Permissions.CatalogView, Permissions.CatalogPricing, Permissions.AuditView,
        ]),
        new("INVENTORY", "Inventory Manager", "انوینٹری مینیجر", "Manages stock, purchases and suppliers",
        [
            Permissions.DashboardView, Permissions.InventoryView, Permissions.InventoryManage,
            Permissions.InventoryPurchase, Permissions.InventoryAdjust, Permissions.CatalogView,
        ]),
        new("CORPBILLING", "Corporate Billing Officer", "کارپوریٹ بلنگ آفیسر", "Corporate clients, credit invoices and receipts",
        [
            Permissions.DashboardView, Permissions.PatientsView, Permissions.OrdersView, Permissions.BillingView,
            Permissions.CorporateView, Permissions.CorporateManage, Permissions.CorporateInvoice,
            Permissions.CorporateReceive, Permissions.ReportsView,
        ]),
    ];
}
