IF OBJECT_ID(N'[core].[__EFMigrationsHistory]') IS NULL
BEGIN
    IF SCHEMA_ID(N'core') IS NULL EXEC(N'CREATE SCHEMA [core];');
    CREATE TABLE [core].[__EFMigrationsHistory] (
        [MigrationId] nvarchar(150) NOT NULL,
        [ProductVersion] nvarchar(32) NOT NULL,
        CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY ([MigrationId])
    );
END;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [core].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261109060843_InitialFoundation'
)
BEGIN
    IF SCHEMA_ID(N'audit') IS NULL EXEC(N'CREATE SCHEMA [audit];');
END;

IF NOT EXISTS (
    SELECT * FROM [core].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261109060843_InitialFoundation'
)
BEGIN
    IF SCHEMA_ID(N'core') IS NULL EXEC(N'CREATE SCHEMA [core];');
END;

IF NOT EXISTS (
    SELECT * FROM [core].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261109060843_InitialFoundation'
)
BEGIN
    IF SCHEMA_ID(N'security') IS NULL EXEC(N'CREATE SCHEMA [security];');
END;

IF NOT EXISTS (
    SELECT * FROM [core].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261109060843_InitialFoundation'
)
BEGIN
    IF SCHEMA_ID(N'catalog') IS NULL EXEC(N'CREATE SCHEMA [catalog];');
END;

IF NOT EXISTS (
    SELECT * FROM [core].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261109060843_InitialFoundation'
)
BEGIN
    CREATE TABLE [audit].[AuditLogs] (
        [Id] bigint NOT NULL IDENTITY,
        [OrganizationId] int NULL,
        [BranchId] int NULL,
        [UserId] int NULL,
        [Action] varchar(64) NOT NULL,
        [EntityType] varchar(128) NOT NULL,
        [EntityId] varchar(64) NOT NULL,
        [Changes] nvarchar(max) NULL,
        [Reason] nvarchar(500) NULL,
        [IpAddress] varchar(45) NULL,
        [CorrelationId] varchar(64) NULL,
        [AtUtc] datetime2(3) NOT NULL,
        CONSTRAINT [PK_AuditLogs] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [core].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261109060843_InitialFoundation'
)
BEGIN
    CREATE TABLE [security].[LoginHistory] (
        [Id] bigint NOT NULL IDENTITY,
        [OrganizationId] int NULL,
        [UserId] int NULL,
        [UsernameAttempted] nvarchar(64) NOT NULL,
        [Succeeded] bit NOT NULL,
        [FailureReason] nvarchar(100) NULL,
        [IpAddress] varchar(45) NULL,
        [UserAgent] nvarchar(512) NULL,
        [AtUtc] datetime2(3) NOT NULL,
        CONSTRAINT [PK_LoginHistory] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [core].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261109060843_InitialFoundation'
)
BEGIN
    CREATE TABLE [core].[Organizations] (
        [Id] int NOT NULL IDENTITY,
        [Code] varchar(20) NOT NULL,
        [NameEn] nvarchar(200) NOT NULL,
        [NameUr] nvarchar(200) NULL,
        [Phone] nvarchar(30) NULL,
        [Email] nvarchar(256) NULL,
        [AddressEn] nvarchar(500) NULL,
        [AddressUr] nvarchar(500) NULL,
        [IsActive] bit NOT NULL,
        [CreatedAtUtc] datetime2(3) NOT NULL,
        [CreatedByUserId] int NULL,
        [UpdatedAtUtc] datetime2(3) NULL,
        [UpdatedByUserId] int NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_Organizations] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [core].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261109060843_InitialFoundation'
)
BEGIN
    CREATE TABLE [security].[Permissions] (
        [Id] int NOT NULL,
        [Key] varchar(64) NOT NULL,
        [Module] nvarchar(40) NOT NULL,
        [DescriptionEn] nvarchar(200) NOT NULL,
        [DescriptionUr] nvarchar(200) NULL,
        CONSTRAINT [PK_Permissions] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [core].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261109060843_InitialFoundation'
)
BEGIN
    CREATE TABLE [catalog].[SampleTypes] (
        [Id] int NOT NULL IDENTITY,
        [Code] varchar(20) NOT NULL,
        [NameEn] nvarchar(200) NOT NULL,
        [NameUr] nvarchar(200) NULL,
        [Container] nvarchar(100) NULL,
        [CapColour] nvarchar(30) NULL,
        [IsActive] bit NOT NULL,
        CONSTRAINT [PK_SampleTypes] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [core].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261109060843_InitialFoundation'
)
BEGIN
    CREATE TABLE [catalog].[Units] (
        [Id] int NOT NULL IDENTITY,
        [Code] nvarchar(30) COLLATE Latin1_General_100_CS_AS NOT NULL,
        [Name] nvarchar(100) NOT NULL,
        [IsActive] bit NOT NULL,
        CONSTRAINT [PK_Units] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [core].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261109060843_InitialFoundation'
)
BEGIN
    CREATE TABLE [core].[Branches] (
        [Id] int NOT NULL IDENTITY,
        [OrganizationId] int NOT NULL,
        [Code] varchar(20) NOT NULL,
        [NameEn] nvarchar(200) NOT NULL,
        [NameUr] nvarchar(200) NULL,
        [Phone] nvarchar(30) NULL,
        [Email] nvarchar(256) NULL,
        [AddressEn] nvarchar(500) NULL,
        [AddressUr] nvarchar(500) NULL,
        [IsMain] bit NOT NULL,
        [IsActive] bit NOT NULL,
        [CreatedAtUtc] datetime2(3) NOT NULL,
        [CreatedByUserId] int NULL,
        [UpdatedAtUtc] datetime2(3) NULL,
        [UpdatedByUserId] int NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_Branches] PRIMARY KEY ([Id]),
        CONSTRAINT [AK_Branches_OrganizationId_Id] UNIQUE ([OrganizationId], [Id]),
        CONSTRAINT [FK_Branches_Organizations_OrganizationId] FOREIGN KEY ([OrganizationId]) REFERENCES [core].[Organizations] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [core].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261109060843_InitialFoundation'
)
BEGIN
    CREATE TABLE [core].[Departments] (
        [Id] int NOT NULL IDENTITY,
        [OrganizationId] int NOT NULL,
        [Code] varchar(20) NOT NULL,
        [NameEn] nvarchar(200) NOT NULL,
        [NameUr] nvarchar(200) NULL,
        [SortOrder] int NOT NULL,
        [IsActive] bit NOT NULL,
        [CreatedAtUtc] datetime2(3) NOT NULL,
        [CreatedByUserId] int NULL,
        [UpdatedAtUtc] datetime2(3) NULL,
        [UpdatedByUserId] int NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_Departments] PRIMARY KEY ([Id]),
        CONSTRAINT [AK_Departments_OrganizationId_Id] UNIQUE ([OrganizationId], [Id]),
        CONSTRAINT [FK_Departments_Organizations_OrganizationId] FOREIGN KEY ([OrganizationId]) REFERENCES [core].[Organizations] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [core].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261109060843_InitialFoundation'
)
BEGIN
    CREATE TABLE [security].[Roles] (
        [Id] int NOT NULL IDENTITY,
        [OrganizationId] int NOT NULL,
        [Code] varchar(20) NOT NULL,
        [NameEn] nvarchar(200) NOT NULL,
        [NameUr] nvarchar(200) NULL,
        [Description] nvarchar(500) NULL,
        [IsSystem] bit NOT NULL,
        [IsActive] bit NOT NULL,
        [CreatedAtUtc] datetime2(3) NOT NULL,
        [CreatedByUserId] int NULL,
        [UpdatedAtUtc] datetime2(3) NULL,
        [UpdatedByUserId] int NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_Roles] PRIMARY KEY ([Id]),
        CONSTRAINT [AK_Roles_OrganizationId_Id] UNIQUE ([OrganizationId], [Id]),
        CONSTRAINT [FK_Roles_Organizations_OrganizationId] FOREIGN KEY ([OrganizationId]) REFERENCES [core].[Organizations] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [core].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261109060843_InitialFoundation'
)
BEGIN
    CREATE TABLE [core].[BusinessSettings] (
        [Id] int NOT NULL IDENTITY,
        [OrganizationId] int NOT NULL,
        [BranchId] int NULL,
        [Key] varchar(100) NOT NULL,
        [Value] nvarchar(4000) NOT NULL,
        [CreatedAtUtc] datetime2(3) NOT NULL,
        [CreatedByUserId] int NULL,
        [UpdatedAtUtc] datetime2(3) NULL,
        [UpdatedByUserId] int NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_BusinessSettings] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_BusinessSettings_Branches_OrganizationId_BranchId] FOREIGN KEY ([OrganizationId], [BranchId]) REFERENCES [core].[Branches] ([OrganizationId], [Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_BusinessSettings_Organizations_OrganizationId] FOREIGN KEY ([OrganizationId]) REFERENCES [core].[Organizations] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [core].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261109060843_InitialFoundation'
)
BEGIN
    CREATE TABLE [core].[NumberSequences] (
        [Id] int NOT NULL IDENTITY,
        [OrganizationId] int NOT NULL,
        [BranchId] int NULL,
        [SequenceType] varchar(40) NOT NULL,
        [Prefix] varchar(10) NOT NULL,
        [Padding] int NOT NULL,
        [ResetPolicy] varchar(10) NOT NULL,
        [CurrentPeriod] varchar(8) NOT NULL,
        [NextValue] bigint NOT NULL,
        [CreatedAtUtc] datetime2(3) NOT NULL,
        [CreatedByUserId] int NULL,
        [UpdatedAtUtc] datetime2(3) NULL,
        [UpdatedByUserId] int NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_NumberSequences] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_NumberSequences_NextValue] CHECK ([NextValue] >= 1),
        CONSTRAINT [CK_NumberSequences_Padding] CHECK ([Padding] BETWEEN 1 AND 12),
        CONSTRAINT [FK_NumberSequences_Branches_OrganizationId_BranchId] FOREIGN KEY ([OrganizationId], [BranchId]) REFERENCES [core].[Branches] ([OrganizationId], [Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_NumberSequences_Organizations_OrganizationId] FOREIGN KEY ([OrganizationId]) REFERENCES [core].[Organizations] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [core].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261109060843_InitialFoundation'
)
BEGIN
    CREATE TABLE [security].[Users] (
        [Id] int NOT NULL IDENTITY,
        [OrganizationId] int NOT NULL,
        [Username] nvarchar(64) NOT NULL,
        [DisplayNameEn] nvarchar(200) NOT NULL,
        [DisplayNameUr] nvarchar(200) NULL,
        [Email] nvarchar(256) NULL,
        [Phone] nvarchar(30) NULL,
        [Designation] nvarchar(200) NULL,
        [PasswordHash] varchar(512) NOT NULL,
        [SecurityStamp] varchar(64) NOT NULL,
        [IsActive] bit NOT NULL,
        [MustChangePassword] bit NOT NULL,
        [AccessFailedCount] int NOT NULL,
        [LockoutEndUtc] datetime2(3) NULL,
        [LastLoginAtUtc] datetime2(3) NULL,
        [PreferredLanguage] varchar(2) NOT NULL,
        [DefaultBranchId] int NULL,
        [AllBranches] bit NOT NULL,
        [CreatedAtUtc] datetime2(3) NOT NULL,
        [CreatedByUserId] int NULL,
        [UpdatedAtUtc] datetime2(3) NULL,
        [UpdatedByUserId] int NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_Users] PRIMARY KEY ([Id]),
        CONSTRAINT [AK_Users_OrganizationId_Id] UNIQUE ([OrganizationId], [Id]),
        CONSTRAINT [CK_Users_PreferredLanguage] CHECK ([PreferredLanguage] IN ('en','ur')),
        CONSTRAINT [FK_Users_Branches_OrganizationId_DefaultBranchId] FOREIGN KEY ([OrganizationId], [DefaultBranchId]) REFERENCES [core].[Branches] ([OrganizationId], [Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Users_Organizations_OrganizationId] FOREIGN KEY ([OrganizationId]) REFERENCES [core].[Organizations] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [core].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261109060843_InitialFoundation'
)
BEGIN
    CREATE TABLE [security].[RolePermissions] (
        [RoleId] int NOT NULL,
        [PermissionId] int NOT NULL,
        [OrganizationId] int NOT NULL,
        CONSTRAINT [PK_RolePermissions] PRIMARY KEY ([RoleId], [PermissionId]),
        CONSTRAINT [FK_RolePermissions_Permissions_PermissionId] FOREIGN KEY ([PermissionId]) REFERENCES [security].[Permissions] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_RolePermissions_Roles_OrganizationId_RoleId] FOREIGN KEY ([OrganizationId], [RoleId]) REFERENCES [security].[Roles] ([OrganizationId], [Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [core].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261109060843_InitialFoundation'
)
BEGIN
    CREATE TABLE [security].[UserBranches] (
        [UserId] int NOT NULL,
        [BranchId] int NOT NULL,
        [OrganizationId] int NOT NULL,
        CONSTRAINT [PK_UserBranches] PRIMARY KEY ([UserId], [BranchId]),
        CONSTRAINT [FK_UserBranches_Branches_OrganizationId_BranchId] FOREIGN KEY ([OrganizationId], [BranchId]) REFERENCES [core].[Branches] ([OrganizationId], [Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_UserBranches_Users_OrganizationId_UserId] FOREIGN KEY ([OrganizationId], [UserId]) REFERENCES [security].[Users] ([OrganizationId], [Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [core].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261109060843_InitialFoundation'
)
BEGIN
    CREATE TABLE [security].[UserRoles] (
        [UserId] int NOT NULL,
        [RoleId] int NOT NULL,
        [OrganizationId] int NOT NULL,
        CONSTRAINT [PK_UserRoles] PRIMARY KEY ([UserId], [RoleId]),
        CONSTRAINT [FK_UserRoles_Roles_OrganizationId_RoleId] FOREIGN KEY ([OrganizationId], [RoleId]) REFERENCES [security].[Roles] ([OrganizationId], [Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_UserRoles_Users_OrganizationId_UserId] FOREIGN KEY ([OrganizationId], [UserId]) REFERENCES [security].[Users] ([OrganizationId], [Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [core].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261109060843_InitialFoundation'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'DescriptionEn', N'DescriptionUr', N'Key', N'Module') AND [object_id] = OBJECT_ID(N'[security].[Permissions]'))
        SET IDENTITY_INSERT [security].[Permissions] ON;
    EXEC(N'INSERT INTO [security].[Permissions] ([Id], [DescriptionEn], [DescriptionUr], [Key], [Module])
    VALUES (1, N''View dashboard and statistics'', N''ڈیش بورڈ اور اعداد و شمار دیکھیں'', ''dashboard.view'', N''Dashboard''),
    (2, N''Search and view patients'', N''مریض تلاش کریں اور دیکھیں'', ''patients.view'', N''Patients''),
    (3, N''Register patients'', N''مریضوں کا اندراج'', ''patients.create'', N''Patients''),
    (4, N''Edit patient details'', N''مریض کی تفصیلات میں ترمیم'', ''patients.edit'', N''Patients''),
    (5, N''View national ID and full contact details'', N''شناختی کارڈ اور مکمل رابطہ تفصیلات دیکھیں'', ''patients.viewSensitive'', N''Patients''),
    (6, N''Merge duplicate patient records'', N''دہرے ریکارڈ یکجا کریں'', ''patients.merge'', N''Patients''),
    (7, N''View doctors'', N''ڈاکٹرز دیکھیں'', ''doctors.view'', N''Patients''),
    (8, N''Manage doctors and referrers'', N''ڈاکٹرز کا انتظام'', ''doctors.manage'', N''Patients''),
    (9, N''View lab orders'', N''لیب آرڈرز دیکھیں'', ''orders.view'', N''Orders''),
    (10, N''Create lab orders'', N''لیب آرڈر بنائیں'', ''orders.create'', N''Orders''),
    (11, N''Cancel orders or tests'', N''آرڈر یا ٹیسٹ منسوخ کریں'', ''orders.cancel'', N''Orders''),
    (12, N''View invoices'', N''انوائسز دیکھیں'', ''billing.view'', N''Billing''),
    (13, N''Apply discounts'', N''رعایت دیں'', ''billing.discount'', N''Billing''),
    (14, N''Issue refunds and reversals'', N''رقم کی واپسی اور منسوخی'', ''billing.refund'', N''Billing''),
    (15, N''Collect payments and print receipts'', N''ادائیگی وصول کریں اور رسید پرنٹ کریں'', ''payments.collect'', N''Billing''),
    (16, N''Close cash drawer and day'', N''کیش دراز اور دن بند کریں'', ''cash.close'', N''Billing''),
    (17, N''View corporate clients and balances'', N''کارپوریٹ کلائنٹس اور بیلنس دیکھیں'', ''corporate.view'', N''Corporate''),
    (18, N''Manage clients, contracts and price lists'', N''کلائنٹس، معاہدوں اور قیمتوں کا انتظام'', ''corporate.manage'', N''Corporate''),
    (19, N''Generate corporate credit invoices'', N''کارپوریٹ ادھار انوائس بنائیں'', ''corporate.invoice'', N''Corporate''),
    (20, N''Record corporate payments and allocations'', N''کارپوریٹ ادائیگیاں درج کریں'', ''corporate.receive'', N''Corporate''),
    (21, N''Authorise credit adjustments'', N''ادھار میں ترمیم کی منظوری'', ''corporate.adjust'', N''Corporate''),
    (22, N''View and track samples'', N''نمونے دیکھیں اور ٹریک کریں'', ''samples.view'', N''Samples''),
    (23, N''Collect samples and print barcodes'', N''نمونے لیں اور بارکوڈ پرنٹ کریں'', ''samples.collect'', N''Samples''),
    (24, N''Receive samples in the laboratory'', N''لیبارٹری میں نمونے وصول کریں'', ''samples.receive'', N''Samples''),
    (25, N''Reject samples and request recollection'', N''نمونہ مسترد کریں اور دوبارہ طلب کریں'', ''samples.reject'', N''Samples''),
    (26, N''Transfer samples between branches'', N''برانچز کے درمیان نمونہ منتقل کریں'', ''samples.transfer'', N''Samples''),
    (27, N''View results'', N''نتائج دیکھیں'', ''results.view'', N''Results''),
    (28, N''Enter and save results'', N''نتائج درج کریں'', ''results.enter'', N''Results''),
    (29, N''Technically verify results'', N''نتائج کی تکنیکی تصدیق'', ''results.verify'', N''Results''),
    (30, N''Approve and release results'', N''نتائج کی منظوری اور اجرا'', ''results.approve'', N''Results''),
    (31, N''Amend approved results (with reason)'', N''منظور شدہ نتائج میں ترمیم (وجہ کے ساتھ)'', ''results.amend'', N''Results''),
    (32, N''Handle critical value alerts'', N''تشویشناک نتائج کے الرٹس نمٹائیں'', ''results.critical'', N''Results''),
    (33, N''View laboratory reports'', N''لیبارٹری رپورٹس دیکھیں'', ''reports.view'', N''Reports''),
    (34, N''Print and export reports'', N''رپورٹس پرنٹ اور ایکسپورٹ کریں'', ''reports.print'', N''Reports''),
    (35, N''View test catalog'', N''ٹیسٹ کیٹلاگ دیکھیں'', ''catalog.view'', N''Catalog''),
    (36, N''Manage tests, parameters and packages'', N''ٹیسٹ، پیرامیٹرز اور پیکجز کا انتظام'', ''catalog.manage'', N''Catalog''),
    (37, N''Approve reference ranges and critical limits'', N''ریفرنس رینجز اور تشویشناک حدود کی منظوری'', ''catalog.clinical'', N''Catalog''),
    (38, N''Manage test prices'', N''ٹیسٹ کی قیمتوں کا انتظام'', ''catalog.pricing'', N''Catalog''),
    (39, N''View stock and ledger'', N''اسٹاک اور لیجر دیکھیں'', ''inventory.view'', N''Inventory''),
    (40, N''Manage items and suppliers'', N''اشیاء اور سپلائرز کا انتظام'', ''inventory.manage'', N''Inventory''),
    (41, N''Record purchases and stock receipts'', N''خریداری اور اسٹاک کی وصولی درج کریں'', ''inventory.purchase'', N''Inventory''),
    (42, N''Adjust and transfer stock'', N''اسٹاک میں ترمیم اور منتقلی'', ''inventory.adjust'', N''Inventory'');
    INSERT INTO [security].[Permissions] ([Id], [DescriptionEn], [DescriptionUr], [Key], [Module])
    VALUES (43, N''Manage organisation, branches and departments'', N''ادارہ، برانچز اور شعبہ جات کا انتظام'', ''org.manage'', N''Administration''),
    (44, N''Manage users'', N''صارفین کا انتظام'', ''users.manage'', N''Administration''),
    (45, N''Manage roles and permissions'', N''کردار اور اجازتوں کا انتظام'', ''roles.manage'', N''Administration''),
    (46, N''View audit logs'', N''آڈٹ لاگز دیکھیں'', ''audit.view'', N''Administration''),
    (47, N''Change system settings'', N''سسٹم سیٹنگز تبدیل کریں'', ''settings.manage'', N''Administration''),
    (48, N''Run and restore backups'', N''بیک اپ اور بحالی'', ''backup.manage'', N''Administration'')');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'DescriptionEn', N'DescriptionUr', N'Key', N'Module') AND [object_id] = OBJECT_ID(N'[security].[Permissions]'))
        SET IDENTITY_INSERT [security].[Permissions] OFF;
END;

IF NOT EXISTS (
    SELECT * FROM [core].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261109060843_InitialFoundation'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'CapColour', N'Code', N'Container', N'IsActive', N'NameEn', N'NameUr') AND [object_id] = OBJECT_ID(N'[catalog].[SampleTypes]'))
        SET IDENTITY_INSERT [catalog].[SampleTypes] ON;
    EXEC(N'INSERT INTO [catalog].[SampleTypes] ([Id], [CapColour], [Code], [Container], [IsActive], [NameEn], [NameUr])
    VALUES (1, N''Lavender'', ''WB-EDTA'', N''EDTA tube'', CAST(1 AS bit), N''Whole blood (EDTA)'', N''خون (EDTA)''),
    (2, N''Red / Yellow'', ''SERUM'', N''Clot activator / gel tube'', CAST(1 AS bit), N''Serum'', N''سیرم''),
    (3, N''Light blue'', ''PL-CIT'', N''Sodium citrate tube'', CAST(1 AS bit), N''Plasma (sodium citrate)'', N''پلازما (سوڈیم سائٹریٹ)''),
    (4, N''Grey'', ''PL-FLU'', N''Fluoride oxalate tube'', CAST(1 AS bit), N''Plasma (sodium fluoride)'', N''پلازما (سوڈیم فلورائیڈ)''),
    (5, N''Green'', ''PL-HEP'', N''Lithium heparin tube'', CAST(1 AS bit), N''Plasma (heparin)'', N''پلازما (ہیپرین)''),
    (6, N''Green'', ''WB-HEP'', N''Heparin tube / syringe'', CAST(1 AS bit), N''Whole blood (heparin)'', N''خون (ہیپرین)''),
    (7, NULL, ''URINE'', N''Sterile urine container'', CAST(1 AS bit), N''Urine (random)'', N''پیشاب''),
    (8, NULL, ''URINE-24H'', N''24-hour urine container'', CAST(1 AS bit), N''Urine (24-hour)'', N''پیشاب (24 گھنٹے)''),
    (9, NULL, ''STOOL'', N''Stool container'', CAST(1 AS bit), N''Stool'', N''پاخانہ''),
    (10, NULL, ''CSF'', N''Sterile tube'', CAST(1 AS bit), N''Cerebrospinal fluid'', N''دماغی رطوبت (CSF)''),
    (11, NULL, ''SPUTUM'', N''Sterile container'', CAST(1 AS bit), N''Sputum'', N''بلغم''),
    (12, NULL, ''SWAB'', N''Swab with transport medium'', CAST(1 AS bit), N''Swab'', N''سواب''),
    (13, NULL, ''SEMEN'', N''Sterile container'', CAST(1 AS bit), N''Semen'', N''منی''),
    (14, NULL, ''FLUID'', N''Sterile container'', CAST(1 AS bit), N''Body fluid'', N''جسمانی رطوبت''),
    (15, NULL, ''TISSUE'', N''Formalin container'', CAST(1 AS bit), N''Tissue (biopsy)'', N''ٹشو (بائیوپسی)''),
    (16, NULL, ''BLOOD-CULT'', N''Blood culture bottle'', CAST(1 AS bit), N''Blood (culture)'', N''خون (کلچر)'')');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'CapColour', N'Code', N'Container', N'IsActive', N'NameEn', N'NameUr') AND [object_id] = OBJECT_ID(N'[catalog].[SampleTypes]'))
        SET IDENTITY_INSERT [catalog].[SampleTypes] OFF;
END;

IF NOT EXISTS (
    SELECT * FROM [core].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261109060843_InitialFoundation'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'Code', N'IsActive', N'Name') AND [object_id] = OBJECT_ID(N'[catalog].[Units]'))
        SET IDENTITY_INSERT [catalog].[Units] ON;
    EXEC(N'INSERT INTO [catalog].[Units] ([Id], [Code], [IsActive], [Name])
    VALUES (1, N''g/dL'', CAST(1 AS bit), N''grams per decilitre''),
    (2, N''g/L'', CAST(1 AS bit), N''grams per litre''),
    (3, N''mg/dL'', CAST(1 AS bit), N''milligrams per decilitre''),
    (4, N''mg/L'', CAST(1 AS bit), N''milligrams per litre''),
    (5, N''µg/dL'', CAST(1 AS bit), N''micrograms per decilitre''),
    (6, N''µg/L'', CAST(1 AS bit), N''micrograms per litre''),
    (7, N''ng/mL'', CAST(1 AS bit), N''nanograms per millilitre''),
    (8, N''ng/dL'', CAST(1 AS bit), N''nanograms per decilitre''),
    (9, N''pg/mL'', CAST(1 AS bit), N''picograms per millilitre''),
    (10, N''mmol/L'', CAST(1 AS bit), N''millimoles per litre''),
    (11, N''µmol/L'', CAST(1 AS bit), N''micromoles per litre''),
    (12, N''nmol/L'', CAST(1 AS bit), N''nanomoles per litre''),
    (13, N''pmol/L'', CAST(1 AS bit), N''picomoles per litre''),
    (14, N''mEq/L'', CAST(1 AS bit), N''milliequivalents per litre''),
    (15, N''U/L'', CAST(1 AS bit), N''units per litre''),
    (16, N''IU/L'', CAST(1 AS bit), N''international units per litre''),
    (17, N''mIU/mL'', CAST(1 AS bit), N''milli-international units per millilitre''),
    (18, N''µIU/mL'', CAST(1 AS bit), N''micro-international units per millilitre''),
    (19, N''IU/mL'', CAST(1 AS bit), N''international units per millilitre''),
    (20, N''%'', CAST(1 AS bit), N''percent''),
    (21, N''fL'', CAST(1 AS bit), N''femtolitres''),
    (22, N''pg'', CAST(1 AS bit), N''picograms''),
    (23, N''×10³/µL'', CAST(1 AS bit), N''thousand per microlitre''),
    (24, N''×10⁶/µL'', CAST(1 AS bit), N''million per microlitre''),
    (25, N''×10⁹/L'', CAST(1 AS bit), N''10⁹ per litre''),
    (26, N''×10¹²/L'', CAST(1 AS bit), N''10¹² per litre''),
    (27, N''cells/µL'', CAST(1 AS bit), N''cells per microlitre''),
    (28, N''/HPF'', CAST(1 AS bit), N''per high-power field''),
    (29, N''/LPF'', CAST(1 AS bit), N''per low-power field''),
    (30, N''mm/hr'', CAST(1 AS bit), N''millimetres per hour''),
    (31, N''sec'', CAST(1 AS bit), N''seconds''),
    (32, N''min'', CAST(1 AS bit), N''minutes''),
    (33, N''ratio'', CAST(1 AS bit), N''ratio (dimensionless)''),
    (34, N''mL/min/1.73m²'', CAST(1 AS bit), N''millilitres per minute per 1.73 m²''),
    (35, N''mOsm/kg'', CAST(1 AS bit), N''milliosmoles per kilogram''),
    (36, N''copies/mL'', CAST(1 AS bit), N''copies per millilitre''),
    (37, N''titre'', CAST(1 AS bit), N''titre''),
    (38, N''mL'', CAST(1 AS bit), N''millilitres''),
    (39, N''g/24h'', CAST(1 AS bit), N''grams per 24 hours''),
    (40, N''mg/24h'', CAST(1 AS bit), N''milligrams per 24 hours''),
    (41, N''S/CO'', CAST(1 AS bit), N''signal to cut-off ratio''),
    (42, N''COI'', CAST(1 AS bit), N''cut-off index'')');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'Code', N'IsActive', N'Name') AND [object_id] = OBJECT_ID(N'[catalog].[Units]'))
        SET IDENTITY_INSERT [catalog].[Units] OFF;
END;

IF NOT EXISTS (
    SELECT * FROM [core].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261109060843_InitialFoundation'
)
BEGIN
    CREATE INDEX [IX_AuditLogs_EntityType_EntityId] ON [audit].[AuditLogs] ([EntityType], [EntityId]);
END;

IF NOT EXISTS (
    SELECT * FROM [core].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261109060843_InitialFoundation'
)
BEGIN
    CREATE INDEX [IX_AuditLogs_OrganizationId_AtUtc] ON [audit].[AuditLogs] ([OrganizationId], [AtUtc]);
END;

IF NOT EXISTS (
    SELECT * FROM [core].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261109060843_InitialFoundation'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Branches_OrganizationId_Code] ON [core].[Branches] ([OrganizationId], [Code]);
END;

IF NOT EXISTS (
    SELECT * FROM [core].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261109060843_InitialFoundation'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [UX_Branches_OneMainPerOrganization] ON [core].[Branches] ([OrganizationId]) WHERE [IsMain] = 1');
END;

IF NOT EXISTS (
    SELECT * FROM [core].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261109060843_InitialFoundation'
)
BEGIN
    CREATE UNIQUE INDEX [IX_BusinessSettings_OrganizationId_BranchId_Key] ON [core].[BusinessSettings] ([OrganizationId], [BranchId], [Key]);
END;

IF NOT EXISTS (
    SELECT * FROM [core].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261109060843_InitialFoundation'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Departments_OrganizationId_Code] ON [core].[Departments] ([OrganizationId], [Code]);
END;

IF NOT EXISTS (
    SELECT * FROM [core].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261109060843_InitialFoundation'
)
BEGIN
    CREATE INDEX [IX_LoginHistory_AtUtc] ON [security].[LoginHistory] ([AtUtc]);
END;

IF NOT EXISTS (
    SELECT * FROM [core].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261109060843_InitialFoundation'
)
BEGIN
    CREATE INDEX [IX_LoginHistory_UserId_AtUtc] ON [security].[LoginHistory] ([UserId], [AtUtc]);
END;

IF NOT EXISTS (
    SELECT * FROM [core].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261109060843_InitialFoundation'
)
BEGIN
    CREATE UNIQUE INDEX [IX_NumberSequences_OrganizationId_BranchId_SequenceType] ON [core].[NumberSequences] ([OrganizationId], [BranchId], [SequenceType]);
END;

IF NOT EXISTS (
    SELECT * FROM [core].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261109060843_InitialFoundation'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Organizations_Code] ON [core].[Organizations] ([Code]);
END;

IF NOT EXISTS (
    SELECT * FROM [core].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261109060843_InitialFoundation'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Permissions_Key] ON [security].[Permissions] ([Key]);
END;

IF NOT EXISTS (
    SELECT * FROM [core].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261109060843_InitialFoundation'
)
BEGIN
    CREATE INDEX [IX_RolePermissions_OrganizationId_RoleId] ON [security].[RolePermissions] ([OrganizationId], [RoleId]);
END;

IF NOT EXISTS (
    SELECT * FROM [core].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261109060843_InitialFoundation'
)
BEGIN
    CREATE INDEX [IX_RolePermissions_PermissionId] ON [security].[RolePermissions] ([PermissionId]);
END;

IF NOT EXISTS (
    SELECT * FROM [core].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261109060843_InitialFoundation'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Roles_OrganizationId_Code] ON [security].[Roles] ([OrganizationId], [Code]);
END;

IF NOT EXISTS (
    SELECT * FROM [core].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261109060843_InitialFoundation'
)
BEGIN
    CREATE UNIQUE INDEX [IX_SampleTypes_Code] ON [catalog].[SampleTypes] ([Code]);
END;

IF NOT EXISTS (
    SELECT * FROM [core].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261109060843_InitialFoundation'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Units_Code] ON [catalog].[Units] ([Code]);
END;

IF NOT EXISTS (
    SELECT * FROM [core].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261109060843_InitialFoundation'
)
BEGIN
    CREATE INDEX [IX_UserBranches_BranchId] ON [security].[UserBranches] ([BranchId]);
END;

IF NOT EXISTS (
    SELECT * FROM [core].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261109060843_InitialFoundation'
)
BEGIN
    CREATE INDEX [IX_UserBranches_OrganizationId_BranchId] ON [security].[UserBranches] ([OrganizationId], [BranchId]);
END;

IF NOT EXISTS (
    SELECT * FROM [core].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261109060843_InitialFoundation'
)
BEGIN
    CREATE INDEX [IX_UserBranches_OrganizationId_UserId] ON [security].[UserBranches] ([OrganizationId], [UserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [core].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261109060843_InitialFoundation'
)
BEGIN
    CREATE INDEX [IX_UserRoles_OrganizationId_RoleId] ON [security].[UserRoles] ([OrganizationId], [RoleId]);
END;

IF NOT EXISTS (
    SELECT * FROM [core].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261109060843_InitialFoundation'
)
BEGIN
    CREATE INDEX [IX_UserRoles_OrganizationId_UserId] ON [security].[UserRoles] ([OrganizationId], [UserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [core].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261109060843_InitialFoundation'
)
BEGIN
    CREATE INDEX [IX_UserRoles_RoleId] ON [security].[UserRoles] ([RoleId]);
END;

IF NOT EXISTS (
    SELECT * FROM [core].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261109060843_InitialFoundation'
)
BEGIN
    CREATE INDEX [IX_Users_OrganizationId_DefaultBranchId] ON [security].[Users] ([OrganizationId], [DefaultBranchId]);
END;

IF NOT EXISTS (
    SELECT * FROM [core].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261109060843_InitialFoundation'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Users_Username] ON [security].[Users] ([Username]);
END;

IF NOT EXISTS (
    SELECT * FROM [core].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261109060843_InitialFoundation'
)
BEGIN
    DBCC CHECKIDENT ('[catalog].[Units]', RESEED, 999);
END;

IF NOT EXISTS (
    SELECT * FROM [core].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261109060843_InitialFoundation'
)
BEGIN
    DBCC CHECKIDENT ('[catalog].[SampleTypes]', RESEED, 999);
END;

IF NOT EXISTS (
    SELECT * FROM [core].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261109060843_InitialFoundation'
)
BEGIN
    INSERT INTO [core].[__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261109060843_InitialFoundation', N'9.0.10');
END;

IF NOT EXISTS (
    SELECT * FROM [core].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261109092300_AddPatientsAndDoctors'
)
BEGIN
    IF SCHEMA_ID(N'patient') IS NULL EXEC(N'CREATE SCHEMA [patient];');
END;

IF NOT EXISTS (
    SELECT * FROM [core].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261109092300_AddPatientsAndDoctors'
)
BEGIN
    CREATE TABLE [patient].[Doctors] (
        [Id] int NOT NULL IDENTITY,
        [OrganizationId] int NOT NULL,
        [Code] varchar(20) NOT NULL,
        [FullName] nvarchar(200) NOT NULL,
        [FullNameUr] nvarchar(200) NULL,
        [Qualification] nvarchar(200) NULL,
        [Specialty] nvarchar(100) NULL,
        [PmdcNo] varchar(30) NULL,
        [Phone] varchar(20) NULL,
        [Email] nvarchar(256) NULL,
        [Hospital] nvarchar(200) NULL,
        [City] nvarchar(100) NULL,
        [IsInHouse] bit NOT NULL,
        [IsActive] bit NOT NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAtUtc] datetime2(3) NULL,
        [DeletedByUserId] int NULL,
        [CreatedAtUtc] datetime2(3) NOT NULL,
        [CreatedByUserId] int NULL,
        [UpdatedAtUtc] datetime2(3) NULL,
        [UpdatedByUserId] int NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_Doctors] PRIMARY KEY ([Id]),
        CONSTRAINT [AK_Doctors_OrganizationId_Id] UNIQUE ([OrganizationId], [Id]),
        CONSTRAINT [FK_Doctors_Organizations_OrganizationId] FOREIGN KEY ([OrganizationId]) REFERENCES [core].[Organizations] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [core].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261109092300_AddPatientsAndDoctors'
)
BEGIN
    CREATE TABLE [patient].[Patients] (
        [Id] int NOT NULL IDENTITY,
        [OrganizationId] int NOT NULL,
        [PatientNo] varchar(30) NOT NULL,
        [Title] nvarchar(20) NULL,
        [FullName] nvarchar(200) NOT NULL,
        [FullNameUr] nvarchar(200) NULL,
        [GuardianName] nvarchar(200) NULL,
        [Sex] int NOT NULL,
        [DateOfBirth] date NULL,
        [IsDateOfBirthEstimated] bit NOT NULL,
        [Cnic] varchar(15) NULL,
        [Mobile] varchar(20) NULL,
        [AltPhone] varchar(20) NULL,
        [Email] nvarchar(256) NULL,
        [Address] nvarchar(500) NULL,
        [City] nvarchar(100) NULL,
        [RegisteredBranchId] int NULL,
        [Notes] nvarchar(1000) NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAtUtc] datetime2(3) NULL,
        [DeletedByUserId] int NULL,
        [CreatedAtUtc] datetime2(3) NOT NULL,
        [CreatedByUserId] int NULL,
        [UpdatedAtUtc] datetime2(3) NULL,
        [UpdatedByUserId] int NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_Patients] PRIMARY KEY ([Id]),
        CONSTRAINT [AK_Patients_OrganizationId_Id] UNIQUE ([OrganizationId], [Id]),
        CONSTRAINT [CK_Patients_Sex] CHECK ([Sex] IN (0,1,2,3)),
        CONSTRAINT [FK_Patients_Branches_OrganizationId_RegisteredBranchId] FOREIGN KEY ([OrganizationId], [RegisteredBranchId]) REFERENCES [core].[Branches] ([OrganizationId], [Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Patients_Organizations_OrganizationId] FOREIGN KEY ([OrganizationId]) REFERENCES [core].[Organizations] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [core].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261109092300_AddPatientsAndDoctors'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Doctors_OrganizationId_Code] ON [patient].[Doctors] ([OrganizationId], [Code]);
END;

IF NOT EXISTS (
    SELECT * FROM [core].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261109092300_AddPatientsAndDoctors'
)
BEGIN
    CREATE INDEX [IX_Doctors_OrganizationId_FullName] ON [patient].[Doctors] ([OrganizationId], [FullName]);
END;

IF NOT EXISTS (
    SELECT * FROM [core].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261109092300_AddPatientsAndDoctors'
)
BEGIN
    CREATE INDEX [IX_Patients_OrganizationId_Cnic] ON [patient].[Patients] ([OrganizationId], [Cnic]);
END;

IF NOT EXISTS (
    SELECT * FROM [core].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261109092300_AddPatientsAndDoctors'
)
BEGIN
    CREATE INDEX [IX_Patients_OrganizationId_FullName] ON [patient].[Patients] ([OrganizationId], [FullName]);
END;

IF NOT EXISTS (
    SELECT * FROM [core].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261109092300_AddPatientsAndDoctors'
)
BEGIN
    CREATE INDEX [IX_Patients_OrganizationId_Mobile] ON [patient].[Patients] ([OrganizationId], [Mobile]);
END;

IF NOT EXISTS (
    SELECT * FROM [core].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261109092300_AddPatientsAndDoctors'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Patients_OrganizationId_PatientNo] ON [patient].[Patients] ([OrganizationId], [PatientNo]);
END;

IF NOT EXISTS (
    SELECT * FROM [core].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261109092300_AddPatientsAndDoctors'
)
BEGIN
    CREATE INDEX [IX_Patients_OrganizationId_RegisteredBranchId] ON [patient].[Patients] ([OrganizationId], [RegisteredBranchId]);
END;

IF NOT EXISTS (
    SELECT * FROM [core].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261109092300_AddPatientsAndDoctors'
)
BEGIN
    INSERT INTO [core].[__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261109092300_AddPatientsAndDoctors', N'9.0.10');
END;

IF NOT EXISTS (
    SELECT * FROM [core].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261109094313_AddTestCatalog'
)
BEGIN
    CREATE TABLE [catalog].[TestCategories] (
        [Id] int NOT NULL IDENTITY,
        [OrganizationId] int NOT NULL,
        [Code] varchar(20) NOT NULL,
        [NameEn] nvarchar(200) NOT NULL,
        [NameUr] nvarchar(200) NULL,
        [SortOrder] int NOT NULL,
        [IsActive] bit NOT NULL,
        [CreatedAtUtc] datetime2(3) NOT NULL,
        [CreatedByUserId] int NULL,
        [UpdatedAtUtc] datetime2(3) NULL,
        [UpdatedByUserId] int NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_TestCategories] PRIMARY KEY ([Id]),
        CONSTRAINT [AK_TestCategories_OrganizationId_Id] UNIQUE ([OrganizationId], [Id]),
        CONSTRAINT [FK_TestCategories_Organizations_OrganizationId] FOREIGN KEY ([OrganizationId]) REFERENCES [core].[Organizations] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [core].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261109094313_AddTestCatalog'
)
BEGIN
    CREATE TABLE [catalog].[TestPackages] (
        [Id] int NOT NULL IDENTITY,
        [OrganizationId] int NOT NULL,
        [Code] varchar(20) NOT NULL,
        [NameEn] nvarchar(200) NOT NULL,
        [NameUr] nvarchar(200) NULL,
        [Description] nvarchar(500) NULL,
        [Price] decimal(18,2) NOT NULL,
        [IsActive] bit NOT NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAtUtc] datetime2(3) NULL,
        [DeletedByUserId] int NULL,
        [CreatedAtUtc] datetime2(3) NOT NULL,
        [CreatedByUserId] int NULL,
        [UpdatedAtUtc] datetime2(3) NULL,
        [UpdatedByUserId] int NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_TestPackages] PRIMARY KEY ([Id]),
        CONSTRAINT [AK_TestPackages_OrganizationId_Id] UNIQUE ([OrganizationId], [Id]),
        CONSTRAINT [CK_TestPackages_Price] CHECK ([Price] >= 0),
        CONSTRAINT [FK_TestPackages_Organizations_OrganizationId] FOREIGN KEY ([OrganizationId]) REFERENCES [core].[Organizations] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [core].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261109094313_AddTestCatalog'
)
BEGIN
    CREATE TABLE [catalog].[Tests] (
        [Id] int NOT NULL IDENTITY,
        [OrganizationId] int NOT NULL,
        [Code] varchar(20) NOT NULL,
        [NameEn] nvarchar(200) NOT NULL,
        [NameUr] nvarchar(200) NULL,
        [ShortName] nvarchar(30) NULL,
        [CategoryId] int NOT NULL,
        [DepartmentId] int NOT NULL,
        [SampleTypeId] int NOT NULL,
        [SampleVolume] nvarchar(50) NULL,
        [CollectionNotes] nvarchar(500) NULL,
        [Method] nvarchar(200) NULL,
        [TurnaroundHours] int NOT NULL,
        [Price] decimal(18,2) NOT NULL,
        [ReportNotes] nvarchar(2000) NULL,
        [SortOrder] int NOT NULL,
        [AvailableAllBranches] bit NOT NULL,
        [IsActive] bit NOT NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAtUtc] datetime2(3) NULL,
        [DeletedByUserId] int NULL,
        [CreatedAtUtc] datetime2(3) NOT NULL,
        [CreatedByUserId] int NULL,
        [UpdatedAtUtc] datetime2(3) NULL,
        [UpdatedByUserId] int NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_Tests] PRIMARY KEY ([Id]),
        CONSTRAINT [AK_Tests_OrganizationId_Id] UNIQUE ([OrganizationId], [Id]),
        CONSTRAINT [CK_Tests_Price] CHECK ([Price] >= 0),
        CONSTRAINT [CK_Tests_Turnaround] CHECK ([TurnaroundHours] >= 0),
        CONSTRAINT [FK_Tests_Departments_OrganizationId_DepartmentId] FOREIGN KEY ([OrganizationId], [DepartmentId]) REFERENCES [core].[Departments] ([OrganizationId], [Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Tests_Organizations_OrganizationId] FOREIGN KEY ([OrganizationId]) REFERENCES [core].[Organizations] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Tests_SampleTypes_SampleTypeId] FOREIGN KEY ([SampleTypeId]) REFERENCES [catalog].[SampleTypes] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Tests_TestCategories_OrganizationId_CategoryId] FOREIGN KEY ([OrganizationId], [CategoryId]) REFERENCES [catalog].[TestCategories] ([OrganizationId], [Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [core].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261109094313_AddTestCatalog'
)
BEGIN
    CREATE TABLE [catalog].[PackageTests] (
        [PackageId] int NOT NULL,
        [TestId] int NOT NULL,
        [OrganizationId] int NOT NULL,
        CONSTRAINT [PK_PackageTests] PRIMARY KEY ([PackageId], [TestId]),
        CONSTRAINT [FK_PackageTests_TestPackages_OrganizationId_PackageId] FOREIGN KEY ([OrganizationId], [PackageId]) REFERENCES [catalog].[TestPackages] ([OrganizationId], [Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_PackageTests_Tests_OrganizationId_TestId] FOREIGN KEY ([OrganizationId], [TestId]) REFERENCES [catalog].[Tests] ([OrganizationId], [Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [core].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261109094313_AddTestCatalog'
)
BEGIN
    CREATE TABLE [catalog].[TestBranches] (
        [TestId] int NOT NULL,
        [BranchId] int NOT NULL,
        [OrganizationId] int NOT NULL,
        CONSTRAINT [PK_TestBranches] PRIMARY KEY ([TestId], [BranchId]),
        CONSTRAINT [FK_TestBranches_Branches_OrganizationId_BranchId] FOREIGN KEY ([OrganizationId], [BranchId]) REFERENCES [core].[Branches] ([OrganizationId], [Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_TestBranches_Tests_OrganizationId_TestId] FOREIGN KEY ([OrganizationId], [TestId]) REFERENCES [catalog].[Tests] ([OrganizationId], [Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [core].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261109094313_AddTestCatalog'
)
BEGIN
    CREATE TABLE [catalog].[TestParameters] (
        [Id] int NOT NULL IDENTITY,
        [OrganizationId] int NOT NULL,
        [TestId] int NOT NULL,
        [Code] varchar(20) NOT NULL,
        [NameEn] nvarchar(200) NOT NULL,
        [NameUr] nvarchar(200) NULL,
        [ResultType] int NOT NULL,
        [UnitId] int NULL,
        [DecimalPlaces] int NOT NULL,
        [GroupName] nvarchar(100) NULL,
        [SortOrder] int NOT NULL,
        [IsActive] bit NOT NULL,
        [CreatedAtUtc] datetime2(3) NOT NULL,
        [CreatedByUserId] int NULL,
        [UpdatedAtUtc] datetime2(3) NULL,
        [UpdatedByUserId] int NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_TestParameters] PRIMARY KEY ([Id]),
        CONSTRAINT [AK_TestParameters_OrganizationId_Id] UNIQUE ([OrganizationId], [Id]),
        CONSTRAINT [CK_TestParameters_Decimals] CHECK ([DecimalPlaces] BETWEEN 0 AND 6),
        CONSTRAINT [CK_TestParameters_ResultType] CHECK ([ResultType] BETWEEN 1 AND 4),
        CONSTRAINT [FK_TestParameters_Tests_OrganizationId_TestId] FOREIGN KEY ([OrganizationId], [TestId]) REFERENCES [catalog].[Tests] ([OrganizationId], [Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_TestParameters_Units_UnitId] FOREIGN KEY ([UnitId]) REFERENCES [catalog].[Units] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [core].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261109094313_AddTestCatalog'
)
BEGIN
    CREATE TABLE [catalog].[ParameterOptions] (
        [Id] int NOT NULL IDENTITY,
        [OrganizationId] int NOT NULL,
        [ParameterId] int NOT NULL,
        [ValueEn] nvarchar(100) NOT NULL,
        [ValueUr] nvarchar(100) NULL,
        [IsAbnormal] bit NOT NULL,
        [SortOrder] int NOT NULL,
        CONSTRAINT [PK_ParameterOptions] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ParameterOptions_TestParameters_OrganizationId_ParameterId] FOREIGN KEY ([OrganizationId], [ParameterId]) REFERENCES [catalog].[TestParameters] ([OrganizationId], [Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [core].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261109094313_AddTestCatalog'
)
BEGIN
    CREATE TABLE [catalog].[ReferenceRanges] (
        [Id] int NOT NULL IDENTITY,
        [OrganizationId] int NOT NULL,
        [ParameterId] int NOT NULL,
        [Sex] int NOT NULL,
        [AgeFromDays] int NOT NULL,
        [AgeToDays] int NULL,
        [Low] decimal(18,6) NULL,
        [High] decimal(18,6) NULL,
        [CriticalLow] decimal(18,6) NULL,
        [CriticalHigh] decimal(18,6) NULL,
        [DisplayText] nvarchar(200) NULL,
        [Notes] nvarchar(500) NULL,
        [Source] nvarchar(300) NULL,
        [Status] int NOT NULL,
        [ReplacesRangeId] int NULL,
        [ApprovedByUserId] int NULL,
        [ApprovedAtUtc] datetime2(3) NULL,
        [RetiredByUserId] int NULL,
        [RetiredAtUtc] datetime2(3) NULL,
        [CreatedAtUtc] datetime2(3) NOT NULL,
        [CreatedByUserId] int NULL,
        [UpdatedAtUtc] datetime2(3) NULL,
        [UpdatedByUserId] int NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_ReferenceRanges] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_ReferenceRanges_Age] CHECK ([AgeFromDays] >= 0 AND ([AgeToDays] IS NULL OR [AgeToDays] > [AgeFromDays])),
        CONSTRAINT [CK_ReferenceRanges_Approval] CHECK ([Status] = 0 OR ([ApprovedByUserId] IS NOT NULL AND [ApprovedAtUtc] IS NOT NULL)),
        CONSTRAINT [CK_ReferenceRanges_LowHigh] CHECK ([Low] IS NULL OR [High] IS NULL OR [Low] <= [High]),
        CONSTRAINT [CK_ReferenceRanges_Sex] CHECK ([Sex] BETWEEN 0 AND 2),
        CONSTRAINT [CK_ReferenceRanges_Status] CHECK ([Status] BETWEEN 0 AND 2),
        CONSTRAINT [FK_ReferenceRanges_ReferenceRanges_ReplacesRangeId] FOREIGN KEY ([ReplacesRangeId]) REFERENCES [catalog].[ReferenceRanges] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_ReferenceRanges_TestParameters_OrganizationId_ParameterId] FOREIGN KEY ([OrganizationId], [ParameterId]) REFERENCES [catalog].[TestParameters] ([OrganizationId], [Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [core].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261109094313_AddTestCatalog'
)
BEGIN
    CREATE INDEX [IX_PackageTests_OrganizationId_PackageId] ON [catalog].[PackageTests] ([OrganizationId], [PackageId]);
END;

IF NOT EXISTS (
    SELECT * FROM [core].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261109094313_AddTestCatalog'
)
BEGIN
    CREATE INDEX [IX_PackageTests_OrganizationId_TestId] ON [catalog].[PackageTests] ([OrganizationId], [TestId]);
END;

IF NOT EXISTS (
    SELECT * FROM [core].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261109094313_AddTestCatalog'
)
BEGIN
    CREATE INDEX [IX_ParameterOptions_OrganizationId_ParameterId] ON [catalog].[ParameterOptions] ([OrganizationId], [ParameterId]);
END;

IF NOT EXISTS (
    SELECT * FROM [core].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261109094313_AddTestCatalog'
)
BEGIN
    CREATE UNIQUE INDEX [IX_ParameterOptions_ParameterId_ValueEn] ON [catalog].[ParameterOptions] ([ParameterId], [ValueEn]);
END;

IF NOT EXISTS (
    SELECT * FROM [core].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261109094313_AddTestCatalog'
)
BEGIN
    CREATE INDEX [IX_ReferenceRanges_OrganizationId_ParameterId] ON [catalog].[ReferenceRanges] ([OrganizationId], [ParameterId]);
END;

IF NOT EXISTS (
    SELECT * FROM [core].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261109094313_AddTestCatalog'
)
BEGIN
    CREATE INDEX [IX_ReferenceRanges_OrganizationId_Status] ON [catalog].[ReferenceRanges] ([OrganizationId], [Status]);
END;

IF NOT EXISTS (
    SELECT * FROM [core].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261109094313_AddTestCatalog'
)
BEGIN
    CREATE INDEX [IX_ReferenceRanges_ParameterId_Status] ON [catalog].[ReferenceRanges] ([ParameterId], [Status]);
END;

IF NOT EXISTS (
    SELECT * FROM [core].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261109094313_AddTestCatalog'
)
BEGIN
    CREATE INDEX [IX_ReferenceRanges_ReplacesRangeId] ON [catalog].[ReferenceRanges] ([ReplacesRangeId]);
END;

IF NOT EXISTS (
    SELECT * FROM [core].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261109094313_AddTestCatalog'
)
BEGIN
    CREATE INDEX [IX_TestBranches_OrganizationId_BranchId] ON [catalog].[TestBranches] ([OrganizationId], [BranchId]);
END;

IF NOT EXISTS (
    SELECT * FROM [core].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261109094313_AddTestCatalog'
)
BEGIN
    CREATE INDEX [IX_TestBranches_OrganizationId_TestId] ON [catalog].[TestBranches] ([OrganizationId], [TestId]);
END;

IF NOT EXISTS (
    SELECT * FROM [core].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261109094313_AddTestCatalog'
)
BEGIN
    CREATE UNIQUE INDEX [IX_TestCategories_OrganizationId_Code] ON [catalog].[TestCategories] ([OrganizationId], [Code]);
END;

IF NOT EXISTS (
    SELECT * FROM [core].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261109094313_AddTestCatalog'
)
BEGIN
    CREATE UNIQUE INDEX [IX_TestPackages_OrganizationId_Code] ON [catalog].[TestPackages] ([OrganizationId], [Code]);
END;

IF NOT EXISTS (
    SELECT * FROM [core].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261109094313_AddTestCatalog'
)
BEGIN
    CREATE INDEX [IX_TestParameters_OrganizationId_TestId] ON [catalog].[TestParameters] ([OrganizationId], [TestId]);
END;

IF NOT EXISTS (
    SELECT * FROM [core].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261109094313_AddTestCatalog'
)
BEGIN
    CREATE UNIQUE INDEX [IX_TestParameters_TestId_Code] ON [catalog].[TestParameters] ([TestId], [Code]);
END;

IF NOT EXISTS (
    SELECT * FROM [core].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261109094313_AddTestCatalog'
)
BEGIN
    CREATE INDEX [IX_TestParameters_UnitId] ON [catalog].[TestParameters] ([UnitId]);
END;

IF NOT EXISTS (
    SELECT * FROM [core].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261109094313_AddTestCatalog'
)
BEGIN
    CREATE INDEX [IX_Tests_OrganizationId_CategoryId] ON [catalog].[Tests] ([OrganizationId], [CategoryId]);
END;

IF NOT EXISTS (
    SELECT * FROM [core].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261109094313_AddTestCatalog'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Tests_OrganizationId_Code] ON [catalog].[Tests] ([OrganizationId], [Code]);
END;

IF NOT EXISTS (
    SELECT * FROM [core].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261109094313_AddTestCatalog'
)
BEGIN
    CREATE INDEX [IX_Tests_OrganizationId_DepartmentId] ON [catalog].[Tests] ([OrganizationId], [DepartmentId]);
END;

IF NOT EXISTS (
    SELECT * FROM [core].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261109094313_AddTestCatalog'
)
BEGIN
    CREATE INDEX [IX_Tests_SampleTypeId] ON [catalog].[Tests] ([SampleTypeId]);
END;

IF NOT EXISTS (
    SELECT * FROM [core].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261109094313_AddTestCatalog'
)
BEGIN
    INSERT INTO [core].[__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261109094313_AddTestCatalog', N'9.0.10');
END;

IF NOT EXISTS (
    SELECT * FROM [core].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261109100152_AddOrdersAndBilling'
)
BEGIN
    IF SCHEMA_ID(N'billing') IS NULL EXEC(N'CREATE SCHEMA [billing];');
END;

IF NOT EXISTS (
    SELECT * FROM [core].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261109100152_AddOrdersAndBilling'
)
BEGIN
    IF SCHEMA_ID(N'lab') IS NULL EXEC(N'CREATE SCHEMA [lab];');
END;

IF NOT EXISTS (
    SELECT * FROM [core].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261109100152_AddOrdersAndBilling'
)
BEGIN
    CREATE TABLE [billing].[CashDrawerSessions] (
        [Id] bigint NOT NULL IDENTITY,
        [OrganizationId] int NOT NULL,
        [BranchId] int NOT NULL,
        [UserId] int NOT NULL,
        [Status] int NOT NULL,
        [OpenedAtUtc] datetime2(3) NOT NULL,
        [OpeningFloat] decimal(18,2) NOT NULL,
        [ClosedAtUtc] datetime2(3) NULL,
        [CashIn] decimal(18,2) NULL,
        [CashOut] decimal(18,2) NULL,
        [ExpectedCash] decimal(18,2) NULL,
        [CountedCash] decimal(18,2) NULL,
        [Variance] decimal(18,2) NULL,
        [Notes] nvarchar(500) NULL,
        [CreatedAtUtc] datetime2(3) NOT NULL,
        [CreatedByUserId] int NULL,
        [UpdatedAtUtc] datetime2(3) NULL,
        [UpdatedByUserId] int NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_CashDrawerSessions] PRIMARY KEY ([Id]),
        CONSTRAINT [AK_CashDrawerSessions_OrganizationId_Id] UNIQUE ([OrganizationId], [Id]),
        CONSTRAINT [CK_CashDrawer_Closed] CHECK ([Status] = 0 OR ([ClosedAtUtc] IS NOT NULL AND [CountedCash] IS NOT NULL AND [ExpectedCash] IS NOT NULL)),
        CONSTRAINT [CK_CashDrawer_Float] CHECK ([OpeningFloat] >= 0),
        CONSTRAINT [FK_CashDrawerSessions_Branches_OrganizationId_BranchId] FOREIGN KEY ([OrganizationId], [BranchId]) REFERENCES [core].[Branches] ([OrganizationId], [Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_CashDrawerSessions_Users_OrganizationId_UserId] FOREIGN KEY ([OrganizationId], [UserId]) REFERENCES [security].[Users] ([OrganizationId], [Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [core].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261109100152_AddOrdersAndBilling'
)
BEGIN
    CREATE TABLE [lab].[Orders] (
        [Id] bigint NOT NULL IDENTITY,
        [OrganizationId] int NOT NULL,
        [BranchId] int NOT NULL,
        [OrderNo] varchar(30) NOT NULL,
        [PatientId] int NOT NULL,
        [ReferringDoctorId] int NULL,
        [Priority] int NOT NULL,
        [ClinicalNotes] nvarchar(1000) NULL,
        [Status] int NOT NULL,
        [IdempotencyKey] uniqueidentifier NOT NULL,
        [CancelledAtUtc] datetime2(3) NULL,
        [CancelledByUserId] int NULL,
        [CancelReason] nvarchar(500) NULL,
        [CreatedAtUtc] datetime2(3) NOT NULL,
        [CreatedByUserId] int NULL,
        [UpdatedAtUtc] datetime2(3) NULL,
        [UpdatedByUserId] int NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_Orders] PRIMARY KEY ([Id]),
        CONSTRAINT [AK_Orders_OrganizationId_Id] UNIQUE ([OrganizationId], [Id]),
        CONSTRAINT [FK_Orders_Branches_OrganizationId_BranchId] FOREIGN KEY ([OrganizationId], [BranchId]) REFERENCES [core].[Branches] ([OrganizationId], [Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Orders_Doctors_OrganizationId_ReferringDoctorId] FOREIGN KEY ([OrganizationId], [ReferringDoctorId]) REFERENCES [patient].[Doctors] ([OrganizationId], [Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Orders_Patients_OrganizationId_PatientId] FOREIGN KEY ([OrganizationId], [PatientId]) REFERENCES [patient].[Patients] ([OrganizationId], [Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [core].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261109100152_AddOrdersAndBilling'
)
BEGIN
    CREATE TABLE [billing].[Invoices] (
        [Id] bigint NOT NULL IDENTITY,
        [OrganizationId] int NOT NULL,
        [BranchId] int NOT NULL,
        [InvoiceNo] varchar(30) NOT NULL,
        [OrderId] bigint NOT NULL,
        [PatientId] int NOT NULL,
        [Gross] decimal(18,2) NOT NULL,
        [DiscountPercent] decimal(5,2) NULL,
        [DiscountAmount] decimal(18,2) NOT NULL,
        [DiscountReason] nvarchar(300) NULL,
        [DiscountByUserId] int NULL,
        [Net] decimal(18,2) NOT NULL,
        [Paid] decimal(18,2) NOT NULL,
        [Refunded] decimal(18,2) NOT NULL,
        [Balance] decimal(18,2) NOT NULL,
        [Status] int NOT NULL,
        [CreatedAtUtc] datetime2(3) NOT NULL,
        [CreatedByUserId] int NULL,
        [UpdatedAtUtc] datetime2(3) NULL,
        [UpdatedByUserId] int NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_Invoices] PRIMARY KEY ([Id]),
        CONSTRAINT [AK_Invoices_OrganizationId_Id] UNIQUE ([OrganizationId], [Id]),
        CONSTRAINT [CK_Invoices_Amounts] CHECK ([Gross] >= 0 AND [DiscountAmount] >= 0 AND [DiscountAmount] <= [Gross] AND [Net] = [Gross] - [DiscountAmount] AND [Paid] >= 0 AND [Refunded] >= 0),
        CONSTRAINT [CK_Invoices_Balance] CHECK ([Balance] = [Net] - ([Paid] - [Refunded])),
        CONSTRAINT [CK_Invoices_DiscountPercent] CHECK ([DiscountPercent] IS NULL OR ([DiscountPercent] > 0 AND [DiscountPercent] <= 100)),
        CONSTRAINT [FK_Invoices_Branches_OrganizationId_BranchId] FOREIGN KEY ([OrganizationId], [BranchId]) REFERENCES [core].[Branches] ([OrganizationId], [Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Invoices_Orders_OrganizationId_OrderId] FOREIGN KEY ([OrganizationId], [OrderId]) REFERENCES [lab].[Orders] ([OrganizationId], [Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Invoices_Patients_OrganizationId_PatientId] FOREIGN KEY ([OrganizationId], [PatientId]) REFERENCES [patient].[Patients] ([OrganizationId], [Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [core].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261109100152_AddOrdersAndBilling'
)
BEGIN
    CREATE TABLE [billing].[InvoiceLines] (
        [Id] bigint NOT NULL IDENTITY,
        [OrganizationId] int NOT NULL,
        [InvoiceId] bigint NOT NULL,
        [Kind] int NOT NULL,
        [TestId] int NULL,
        [PackageId] int NULL,
        [Code] varchar(20) NOT NULL,
        [NameEn] nvarchar(200) NOT NULL,
        [NameUr] nvarchar(200) NULL,
        [Price] decimal(18,2) NOT NULL,
        [IsCancelled] bit NOT NULL,
        [CancelledAtUtc] datetime2(3) NULL,
        [CancelledByUserId] int NULL,
        [CancelReason] nvarchar(500) NULL,
        [CreatedAtUtc] datetime2(3) NOT NULL,
        [CreatedByUserId] int NULL,
        [UpdatedAtUtc] datetime2(3) NULL,
        [UpdatedByUserId] int NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_InvoiceLines] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_InvoiceLines_Kind] CHECK (([Kind] = 1 AND [TestId] IS NOT NULL) OR ([Kind] = 2 AND [PackageId] IS NOT NULL)),
        CONSTRAINT [CK_InvoiceLines_Price] CHECK ([Price] >= 0),
        CONSTRAINT [FK_InvoiceLines_Invoices_OrganizationId_InvoiceId] FOREIGN KEY ([OrganizationId], [InvoiceId]) REFERENCES [billing].[Invoices] ([OrganizationId], [Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_InvoiceLines_TestPackages_OrganizationId_PackageId] FOREIGN KEY ([OrganizationId], [PackageId]) REFERENCES [catalog].[TestPackages] ([OrganizationId], [Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_InvoiceLines_Tests_OrganizationId_TestId] FOREIGN KEY ([OrganizationId], [TestId]) REFERENCES [catalog].[Tests] ([OrganizationId], [Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [core].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261109100152_AddOrdersAndBilling'
)
BEGIN
    CREATE TABLE [billing].[Payments] (
        [Id] bigint NOT NULL IDENTITY,
        [OrganizationId] int NOT NULL,
        [BranchId] int NOT NULL,
        [ReceiptNo] varchar(30) NOT NULL,
        [InvoiceId] bigint NOT NULL,
        [PatientId] int NOT NULL,
        [Method] int NOT NULL,
        [Amount] decimal(18,2) NOT NULL,
        [Reference] nvarchar(100) NULL,
        [ReceivedAtUtc] datetime2(3) NOT NULL,
        [ReceivedByUserId] int NOT NULL,
        [DrawerSessionId] bigint NULL,
        [IdempotencyKey] uniqueidentifier NOT NULL,
        [CreatedAtUtc] datetime2(3) NOT NULL,
        [CreatedByUserId] int NULL,
        [UpdatedAtUtc] datetime2(3) NULL,
        [UpdatedByUserId] int NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_Payments] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_Payments_Amount] CHECK ([Amount] > 0),
        CONSTRAINT [CK_Payments_CashDrawer] CHECK ([Method] <> 1 OR [DrawerSessionId] IS NOT NULL),
        CONSTRAINT [CK_Payments_Method] CHECK ([Method] BETWEEN 1 AND 5),
        CONSTRAINT [FK_Payments_CashDrawerSessions_OrganizationId_DrawerSessionId] FOREIGN KEY ([OrganizationId], [DrawerSessionId]) REFERENCES [billing].[CashDrawerSessions] ([OrganizationId], [Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Payments_Invoices_OrganizationId_InvoiceId] FOREIGN KEY ([OrganizationId], [InvoiceId]) REFERENCES [billing].[Invoices] ([OrganizationId], [Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [core].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261109100152_AddOrdersAndBilling'
)
BEGIN
    CREATE TABLE [billing].[Refunds] (
        [Id] bigint NOT NULL IDENTITY,
        [OrganizationId] int NOT NULL,
        [BranchId] int NOT NULL,
        [RefundNo] varchar(30) NOT NULL,
        [InvoiceId] bigint NOT NULL,
        [Method] int NOT NULL,
        [Amount] decimal(18,2) NOT NULL,
        [Reason] nvarchar(500) NOT NULL,
        [RefundedAtUtc] datetime2(3) NOT NULL,
        [RefundedByUserId] int NOT NULL,
        [DrawerSessionId] bigint NULL,
        [IdempotencyKey] uniqueidentifier NOT NULL,
        [CreatedAtUtc] datetime2(3) NOT NULL,
        [CreatedByUserId] int NULL,
        [UpdatedAtUtc] datetime2(3) NULL,
        [UpdatedByUserId] int NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_Refunds] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_Refunds_Amount] CHECK ([Amount] > 0),
        CONSTRAINT [CK_Refunds_CashDrawer] CHECK ([Method] <> 1 OR [DrawerSessionId] IS NOT NULL),
        CONSTRAINT [FK_Refunds_CashDrawerSessions_OrganizationId_DrawerSessionId] FOREIGN KEY ([OrganizationId], [DrawerSessionId]) REFERENCES [billing].[CashDrawerSessions] ([OrganizationId], [Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Refunds_Invoices_OrganizationId_InvoiceId] FOREIGN KEY ([OrganizationId], [InvoiceId]) REFERENCES [billing].[Invoices] ([OrganizationId], [Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [core].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261109100152_AddOrdersAndBilling'
)
BEGIN
    CREATE TABLE [lab].[OrderItems] (
        [Id] bigint NOT NULL IDENTITY,
        [OrganizationId] int NOT NULL,
        [OrderId] bigint NOT NULL,
        [InvoiceLineId] bigint NOT NULL,
        [TestId] int NOT NULL,
        [TestCode] varchar(20) NOT NULL,
        [TestNameEn] nvarchar(200) NOT NULL,
        [TestNameUr] nvarchar(200) NULL,
        [Status] int NOT NULL,
        [CancelledAtUtc] datetime2(3) NULL,
        [CancelledByUserId] int NULL,
        [CreatedAtUtc] datetime2(3) NOT NULL,
        [CreatedByUserId] int NULL,
        [UpdatedAtUtc] datetime2(3) NULL,
        [UpdatedByUserId] int NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_OrderItems] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_OrderItems_InvoiceLines_InvoiceLineId] FOREIGN KEY ([InvoiceLineId]) REFERENCES [billing].[InvoiceLines] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_OrderItems_Orders_OrganizationId_OrderId] FOREIGN KEY ([OrganizationId], [OrderId]) REFERENCES [lab].[Orders] ([OrganizationId], [Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_OrderItems_Tests_OrganizationId_TestId] FOREIGN KEY ([OrganizationId], [TestId]) REFERENCES [catalog].[Tests] ([OrganizationId], [Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [core].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261109100152_AddOrdersAndBilling'
)
BEGIN
    CREATE INDEX [IX_CashDrawerSessions_OrganizationId_UserId] ON [billing].[CashDrawerSessions] ([OrganizationId], [UserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [core].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261109100152_AddOrdersAndBilling'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [UX_CashDrawer_OneOpenPerUser] ON [billing].[CashDrawerSessions] ([OrganizationId], [BranchId], [UserId]) WHERE [Status] = 0');
END;

IF NOT EXISTS (
    SELECT * FROM [core].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261109100152_AddOrdersAndBilling'
)
BEGIN
    CREATE INDEX [IX_InvoiceLines_OrganizationId_InvoiceId] ON [billing].[InvoiceLines] ([OrganizationId], [InvoiceId]);
END;

IF NOT EXISTS (
    SELECT * FROM [core].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261109100152_AddOrdersAndBilling'
)
BEGIN
    CREATE INDEX [IX_InvoiceLines_OrganizationId_PackageId] ON [billing].[InvoiceLines] ([OrganizationId], [PackageId]);
END;

IF NOT EXISTS (
    SELECT * FROM [core].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261109100152_AddOrdersAndBilling'
)
BEGIN
    CREATE INDEX [IX_InvoiceLines_OrganizationId_TestId] ON [billing].[InvoiceLines] ([OrganizationId], [TestId]);
END;

IF NOT EXISTS (
    SELECT * FROM [core].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261109100152_AddOrdersAndBilling'
)
BEGIN
    CREATE INDEX [IX_Invoices_OrganizationId_BranchId] ON [billing].[Invoices] ([OrganizationId], [BranchId]);
END;

IF NOT EXISTS (
    SELECT * FROM [core].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261109100152_AddOrdersAndBilling'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Invoices_OrganizationId_InvoiceNo] ON [billing].[Invoices] ([OrganizationId], [InvoiceNo]);
END;

IF NOT EXISTS (
    SELECT * FROM [core].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261109100152_AddOrdersAndBilling'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Invoices_OrganizationId_OrderId] ON [billing].[Invoices] ([OrganizationId], [OrderId]);
END;

IF NOT EXISTS (
    SELECT * FROM [core].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261109100152_AddOrdersAndBilling'
)
BEGIN
    CREATE INDEX [IX_Invoices_OrganizationId_PatientId] ON [billing].[Invoices] ([OrganizationId], [PatientId]);
END;

IF NOT EXISTS (
    SELECT * FROM [core].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261109100152_AddOrdersAndBilling'
)
BEGIN
    CREATE INDEX [IX_Invoices_OrganizationId_Status] ON [billing].[Invoices] ([OrganizationId], [Status]);
END;

IF NOT EXISTS (
    SELECT * FROM [core].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261109100152_AddOrdersAndBilling'
)
BEGIN
    CREATE INDEX [IX_OrderItems_InvoiceLineId] ON [lab].[OrderItems] ([InvoiceLineId]);
END;

IF NOT EXISTS (
    SELECT * FROM [core].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261109100152_AddOrdersAndBilling'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_OrderItems_OrderId_TestId] ON [lab].[OrderItems] ([OrderId], [TestId]) WHERE [Status] <> 90');
END;

IF NOT EXISTS (
    SELECT * FROM [core].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261109100152_AddOrdersAndBilling'
)
BEGIN
    CREATE INDEX [IX_OrderItems_OrganizationId_OrderId] ON [lab].[OrderItems] ([OrganizationId], [OrderId]);
END;

IF NOT EXISTS (
    SELECT * FROM [core].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261109100152_AddOrdersAndBilling'
)
BEGIN
    CREATE INDEX [IX_OrderItems_OrganizationId_Status] ON [lab].[OrderItems] ([OrganizationId], [Status]);
END;

IF NOT EXISTS (
    SELECT * FROM [core].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261109100152_AddOrdersAndBilling'
)
BEGIN
    CREATE INDEX [IX_OrderItems_OrganizationId_TestId] ON [lab].[OrderItems] ([OrganizationId], [TestId]);
END;

IF NOT EXISTS (
    SELECT * FROM [core].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261109100152_AddOrdersAndBilling'
)
BEGIN
    CREATE INDEX [IX_Orders_OrganizationId_BranchId_CreatedAtUtc] ON [lab].[Orders] ([OrganizationId], [BranchId], [CreatedAtUtc]);
END;

IF NOT EXISTS (
    SELECT * FROM [core].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261109100152_AddOrdersAndBilling'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Orders_OrganizationId_IdempotencyKey] ON [lab].[Orders] ([OrganizationId], [IdempotencyKey]);
END;

IF NOT EXISTS (
    SELECT * FROM [core].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261109100152_AddOrdersAndBilling'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Orders_OrganizationId_OrderNo] ON [lab].[Orders] ([OrganizationId], [OrderNo]);
END;

IF NOT EXISTS (
    SELECT * FROM [core].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261109100152_AddOrdersAndBilling'
)
BEGIN
    CREATE INDEX [IX_Orders_OrganizationId_PatientId] ON [lab].[Orders] ([OrganizationId], [PatientId]);
END;

IF NOT EXISTS (
    SELECT * FROM [core].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261109100152_AddOrdersAndBilling'
)
BEGIN
    CREATE INDEX [IX_Orders_OrganizationId_ReferringDoctorId] ON [lab].[Orders] ([OrganizationId], [ReferringDoctorId]);
END;

IF NOT EXISTS (
    SELECT * FROM [core].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261109100152_AddOrdersAndBilling'
)
BEGIN
    CREATE INDEX [IX_Payments_OrganizationId_BranchId_ReceivedAtUtc] ON [billing].[Payments] ([OrganizationId], [BranchId], [ReceivedAtUtc]);
END;

IF NOT EXISTS (
    SELECT * FROM [core].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261109100152_AddOrdersAndBilling'
)
BEGIN
    CREATE INDEX [IX_Payments_OrganizationId_DrawerSessionId] ON [billing].[Payments] ([OrganizationId], [DrawerSessionId]);
END;

IF NOT EXISTS (
    SELECT * FROM [core].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261109100152_AddOrdersAndBilling'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Payments_OrganizationId_IdempotencyKey] ON [billing].[Payments] ([OrganizationId], [IdempotencyKey]);
END;

IF NOT EXISTS (
    SELECT * FROM [core].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261109100152_AddOrdersAndBilling'
)
BEGIN
    CREATE INDEX [IX_Payments_OrganizationId_InvoiceId] ON [billing].[Payments] ([OrganizationId], [InvoiceId]);
END;

IF NOT EXISTS (
    SELECT * FROM [core].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261109100152_AddOrdersAndBilling'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Payments_OrganizationId_ReceiptNo] ON [billing].[Payments] ([OrganizationId], [ReceiptNo]);
END;

IF NOT EXISTS (
    SELECT * FROM [core].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261109100152_AddOrdersAndBilling'
)
BEGIN
    CREATE INDEX [IX_Refunds_OrganizationId_DrawerSessionId] ON [billing].[Refunds] ([OrganizationId], [DrawerSessionId]);
END;

IF NOT EXISTS (
    SELECT * FROM [core].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261109100152_AddOrdersAndBilling'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Refunds_OrganizationId_IdempotencyKey] ON [billing].[Refunds] ([OrganizationId], [IdempotencyKey]);
END;

IF NOT EXISTS (
    SELECT * FROM [core].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261109100152_AddOrdersAndBilling'
)
BEGIN
    CREATE INDEX [IX_Refunds_OrganizationId_InvoiceId] ON [billing].[Refunds] ([OrganizationId], [InvoiceId]);
END;

IF NOT EXISTS (
    SELECT * FROM [core].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261109100152_AddOrdersAndBilling'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Refunds_OrganizationId_RefundNo] ON [billing].[Refunds] ([OrganizationId], [RefundNo]);
END;

IF NOT EXISTS (
    SELECT * FROM [core].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261109100152_AddOrdersAndBilling'
)
BEGIN
    INSERT INTO [core].[__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261109100152_AddOrdersAndBilling', N'9.0.10');
END;

IF NOT EXISTS (
    SELECT * FROM [core].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261109103202_HardenOrderBilling'
)
BEGIN
    EXEC(N'ALTER TABLE [billing].[Refunds] ADD CONSTRAINT [CK_Refunds_Method] CHECK ([Method] BETWEEN 1 AND 5)');
END;

IF NOT EXISTS (
    SELECT * FROM [core].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261109103202_HardenOrderBilling'
)
BEGIN
    EXEC(N'ALTER TABLE [lab].[Orders] ADD CONSTRAINT [CK_Orders_Priority] CHECK ([Priority] IN (0,1))');
END;

IF NOT EXISTS (
    SELECT * FROM [core].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261109103202_HardenOrderBilling'
)
BEGIN
    EXEC(N'ALTER TABLE [lab].[Orders] ADD CONSTRAINT [CK_Orders_Status] CHECK ([Status] IN (0,1))');
END;

IF NOT EXISTS (
    SELECT * FROM [core].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261109103202_HardenOrderBilling'
)
BEGIN
    EXEC(N'ALTER TABLE [lab].[OrderItems] ADD CONSTRAINT [CK_OrderItems_Status] CHECK ([Status] IN (0,10,20,30,40,90))');
END;

IF NOT EXISTS (
    SELECT * FROM [core].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261109103202_HardenOrderBilling'
)
BEGIN
    INSERT INTO [core].[__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261109103202_HardenOrderBilling', N'9.0.10');
END;

COMMIT;
GO

