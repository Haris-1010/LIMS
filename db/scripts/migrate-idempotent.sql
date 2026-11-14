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

IF NOT EXISTS (
    SELECT * FROM [core].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261109110026_AddSamples'
)
BEGIN
    ALTER TABLE [lab].[OrderItems] ADD CONSTRAINT [AK_OrderItems_OrganizationId_Id] UNIQUE ([OrganizationId], [Id]);
END;

IF NOT EXISTS (
    SELECT * FROM [core].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261109110026_AddSamples'
)
BEGIN
    CREATE TABLE [lab].[SampleRejectionReasons] (
        [Id] int NOT NULL IDENTITY,
        [OrganizationId] int NOT NULL,
        [Code] varchar(30) NOT NULL,
        [NameEn] nvarchar(200) NOT NULL,
        [NameUr] nvarchar(200) NULL,
        [RequiresNote] bit NOT NULL,
        [IsActive] bit NOT NULL,
        [SortOrder] int NOT NULL,
        [CreatedAtUtc] datetime2(3) NOT NULL,
        [CreatedByUserId] int NULL,
        [UpdatedAtUtc] datetime2(3) NULL,
        [UpdatedByUserId] int NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_SampleRejectionReasons] PRIMARY KEY ([Id]),
        CONSTRAINT [AK_SampleRejectionReasons_OrganizationId_Id] UNIQUE ([OrganizationId], [Id]),
        CONSTRAINT [FK_SampleRejectionReasons_Organizations_OrganizationId] FOREIGN KEY ([OrganizationId]) REFERENCES [core].[Organizations] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [core].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261109110026_AddSamples'
)
BEGIN
    CREATE TABLE [lab].[SampleTransfers] (
        [Id] bigint NOT NULL IDENTITY,
        [OrganizationId] int NOT NULL,
        [TransferNo] varchar(30) NOT NULL,
        [FromBranchId] int NOT NULL,
        [ToBranchId] int NOT NULL,
        [Status] int NOT NULL,
        [DispatchedAtUtc] datetime2(3) NOT NULL,
        [DispatchedByUserId] int NOT NULL,
        [CompletedAtUtc] datetime2(3) NULL,
        [CompletedByUserId] int NULL,
        [Notes] nvarchar(500) NULL,
        [CreatedAtUtc] datetime2(3) NOT NULL,
        [CreatedByUserId] int NULL,
        [UpdatedAtUtc] datetime2(3) NULL,
        [UpdatedByUserId] int NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_SampleTransfers] PRIMARY KEY ([Id]),
        CONSTRAINT [AK_SampleTransfers_OrganizationId_Id] UNIQUE ([OrganizationId], [Id]),
        CONSTRAINT [CK_SampleTransfers_Branches] CHECK ([FromBranchId] <> [ToBranchId]),
        CONSTRAINT [CK_SampleTransfers_Status] CHECK ([Status] IN (0,1,2)),
        CONSTRAINT [FK_SampleTransfers_Branches_OrganizationId_FromBranchId] FOREIGN KEY ([OrganizationId], [FromBranchId]) REFERENCES [core].[Branches] ([OrganizationId], [Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_SampleTransfers_Branches_OrganizationId_ToBranchId] FOREIGN KEY ([OrganizationId], [ToBranchId]) REFERENCES [core].[Branches] ([OrganizationId], [Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [core].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261109110026_AddSamples'
)
BEGIN
    CREATE TABLE [lab].[Samples] (
        [Id] bigint NOT NULL IDENTITY,
        [OrganizationId] int NOT NULL,
        [Barcode] varchar(40) NOT NULL,
        [OrderId] bigint NOT NULL,
        [PatientId] int NOT NULL,
        [SampleTypeId] int NOT NULL,
        [Status] int NOT NULL,
        [Priority] int NOT NULL,
        [OriginBranchId] int NOT NULL,
        [CurrentBranchId] int NOT NULL,
        [DestinationBranchId] int NULL,
        [CollectedAtUtc] datetime2(3) NULL,
        [CollectedByUserId] int NULL,
        [IdentityConfirmed] bit NOT NULL,
        [CollectionNotes] nvarchar(500) NULL,
        [ReceivedAtUtc] datetime2(3) NULL,
        [ReceivedByUserId] int NULL,
        [RejectedAtUtc] datetime2(3) NULL,
        [RejectedByUserId] int NULL,
        [RejectionReasonId] int NULL,
        [RejectionNote] nvarchar(500) NULL,
        [ReplacesSampleId] bigint NULL,
        [CancelledAtUtc] datetime2(3) NULL,
        [LabelPrintCount] int NOT NULL,
        [LastLabelPrintedAtUtc] datetime2(3) NULL,
        [CreatedAtUtc] datetime2(3) NOT NULL,
        [CreatedByUserId] int NULL,
        [UpdatedAtUtc] datetime2(3) NULL,
        [UpdatedByUserId] int NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_Samples] PRIMARY KEY ([Id]),
        CONSTRAINT [AK_Samples_OrganizationId_Id] UNIQUE ([OrganizationId], [Id]),
        CONSTRAINT [CK_Samples_Collected] CHECK ([Status] IN (0,90) OR ([CollectedAtUtc] IS NOT NULL AND [CollectedByUserId] IS NOT NULL)),
        CONSTRAINT [CK_Samples_InTransit] CHECK ([Status] <> 15 OR [DestinationBranchId] IS NOT NULL),
        CONSTRAINT [CK_Samples_Priority] CHECK ([Priority] IN (0,1)),
        CONSTRAINT [CK_Samples_Received] CHECK ([Status] <> 20 OR ([ReceivedAtUtc] IS NOT NULL AND [ReceivedByUserId] IS NOT NULL)),
        CONSTRAINT [CK_Samples_Rejected] CHECK ([Status] <> 80 OR ([RejectedAtUtc] IS NOT NULL AND [RejectedByUserId] IS NOT NULL AND [RejectionReasonId] IS NOT NULL)),
        CONSTRAINT [CK_Samples_Status] CHECK ([Status] IN (0,10,15,20,80,90)),
        CONSTRAINT [FK_Samples_Branches_OrganizationId_CurrentBranchId] FOREIGN KEY ([OrganizationId], [CurrentBranchId]) REFERENCES [core].[Branches] ([OrganizationId], [Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Samples_Branches_OrganizationId_DestinationBranchId] FOREIGN KEY ([OrganizationId], [DestinationBranchId]) REFERENCES [core].[Branches] ([OrganizationId], [Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Samples_Branches_OrganizationId_OriginBranchId] FOREIGN KEY ([OrganizationId], [OriginBranchId]) REFERENCES [core].[Branches] ([OrganizationId], [Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Samples_Orders_OrganizationId_OrderId] FOREIGN KEY ([OrganizationId], [OrderId]) REFERENCES [lab].[Orders] ([OrganizationId], [Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Samples_Patients_OrganizationId_PatientId] FOREIGN KEY ([OrganizationId], [PatientId]) REFERENCES [patient].[Patients] ([OrganizationId], [Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Samples_SampleRejectionReasons_OrganizationId_RejectionReasonId] FOREIGN KEY ([OrganizationId], [RejectionReasonId]) REFERENCES [lab].[SampleRejectionReasons] ([OrganizationId], [Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Samples_SampleTypes_SampleTypeId] FOREIGN KEY ([SampleTypeId]) REFERENCES [catalog].[SampleTypes] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Samples_Samples_OrganizationId_ReplacesSampleId] FOREIGN KEY ([OrganizationId], [ReplacesSampleId]) REFERENCES [lab].[Samples] ([OrganizationId], [Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [core].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261109110026_AddSamples'
)
BEGIN
    CREATE TABLE [lab].[SampleEvents] (
        [Id] bigint NOT NULL IDENTITY,
        [OrganizationId] int NOT NULL,
        [SampleId] bigint NOT NULL,
        [Type] varchar(40) NOT NULL,
        [AtUtc] datetime2(3) NOT NULL,
        [UserId] int NULL,
        [BranchId] int NULL,
        [Notes] nvarchar(500) NULL,
        CONSTRAINT [PK_SampleEvents] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_SampleEvents_Samples_OrganizationId_SampleId] FOREIGN KEY ([OrganizationId], [SampleId]) REFERENCES [lab].[Samples] ([OrganizationId], [Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [core].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261109110026_AddSamples'
)
BEGIN
    CREATE TABLE [lab].[SampleTests] (
        [SampleId] bigint NOT NULL,
        [OrderItemId] bigint NOT NULL,
        [OrganizationId] int NOT NULL,
        CONSTRAINT [PK_SampleTests] PRIMARY KEY ([SampleId], [OrderItemId]),
        CONSTRAINT [FK_SampleTests_OrderItems_OrganizationId_OrderItemId] FOREIGN KEY ([OrganizationId], [OrderItemId]) REFERENCES [lab].[OrderItems] ([OrganizationId], [Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_SampleTests_Samples_OrganizationId_SampleId] FOREIGN KEY ([OrganizationId], [SampleId]) REFERENCES [lab].[Samples] ([OrganizationId], [Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [core].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261109110026_AddSamples'
)
BEGIN
    CREATE TABLE [lab].[SampleTransferItems] (
        [TransferId] bigint NOT NULL,
        [SampleId] bigint NOT NULL,
        [OrganizationId] int NOT NULL,
        [ReceivedAtUtc] datetime2(3) NULL,
        [ReceivedByUserId] int NULL,
        [MarkedMissing] bit NOT NULL,
        CONSTRAINT [PK_SampleTransferItems] PRIMARY KEY ([TransferId], [SampleId]),
        CONSTRAINT [FK_SampleTransferItems_SampleTransfers_OrganizationId_TransferId] FOREIGN KEY ([OrganizationId], [TransferId]) REFERENCES [lab].[SampleTransfers] ([OrganizationId], [Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_SampleTransferItems_Samples_OrganizationId_SampleId] FOREIGN KEY ([OrganizationId], [SampleId]) REFERENCES [lab].[Samples] ([OrganizationId], [Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [core].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261109110026_AddSamples'
)
BEGIN
    CREATE INDEX [IX_SampleEvents_OrganizationId_SampleId] ON [lab].[SampleEvents] ([OrganizationId], [SampleId]);
END;

IF NOT EXISTS (
    SELECT * FROM [core].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261109110026_AddSamples'
)
BEGIN
    CREATE INDEX [IX_SampleEvents_SampleId_Id] ON [lab].[SampleEvents] ([SampleId], [Id]);
END;

IF NOT EXISTS (
    SELECT * FROM [core].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261109110026_AddSamples'
)
BEGIN
    CREATE UNIQUE INDEX [IX_SampleRejectionReasons_OrganizationId_Code] ON [lab].[SampleRejectionReasons] ([OrganizationId], [Code]);
END;

IF NOT EXISTS (
    SELECT * FROM [core].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261109110026_AddSamples'
)
BEGIN
    CREATE INDEX [IX_Samples_OrderId] ON [lab].[Samples] ([OrderId]);
END;

IF NOT EXISTS (
    SELECT * FROM [core].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261109110026_AddSamples'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Samples_OrganizationId_Barcode] ON [lab].[Samples] ([OrganizationId], [Barcode]);
END;

IF NOT EXISTS (
    SELECT * FROM [core].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261109110026_AddSamples'
)
BEGIN
    CREATE INDEX [IX_Samples_OrganizationId_CurrentBranchId] ON [lab].[Samples] ([OrganizationId], [CurrentBranchId]);
END;

IF NOT EXISTS (
    SELECT * FROM [core].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261109110026_AddSamples'
)
BEGIN
    CREATE INDEX [IX_Samples_OrganizationId_DestinationBranchId] ON [lab].[Samples] ([OrganizationId], [DestinationBranchId]);
END;

IF NOT EXISTS (
    SELECT * FROM [core].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261109110026_AddSamples'
)
BEGIN
    CREATE INDEX [IX_Samples_OrganizationId_OrderId] ON [lab].[Samples] ([OrganizationId], [OrderId]);
END;

IF NOT EXISTS (
    SELECT * FROM [core].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261109110026_AddSamples'
)
BEGIN
    CREATE INDEX [IX_Samples_OrganizationId_OriginBranchId] ON [lab].[Samples] ([OrganizationId], [OriginBranchId]);
END;

IF NOT EXISTS (
    SELECT * FROM [core].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261109110026_AddSamples'
)
BEGIN
    CREATE INDEX [IX_Samples_OrganizationId_PatientId] ON [lab].[Samples] ([OrganizationId], [PatientId]);
END;

IF NOT EXISTS (
    SELECT * FROM [core].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261109110026_AddSamples'
)
BEGIN
    CREATE INDEX [IX_Samples_OrganizationId_RejectionReasonId] ON [lab].[Samples] ([OrganizationId], [RejectionReasonId]);
END;

IF NOT EXISTS (
    SELECT * FROM [core].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261109110026_AddSamples'
)
BEGIN
    CREATE INDEX [IX_Samples_OrganizationId_ReplacesSampleId] ON [lab].[Samples] ([OrganizationId], [ReplacesSampleId]);
END;

IF NOT EXISTS (
    SELECT * FROM [core].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261109110026_AddSamples'
)
BEGIN
    CREATE INDEX [IX_Samples_OrganizationId_Status_CurrentBranchId] ON [lab].[Samples] ([OrganizationId], [Status], [CurrentBranchId]);
END;

IF NOT EXISTS (
    SELECT * FROM [core].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261109110026_AddSamples'
)
BEGIN
    CREATE INDEX [IX_Samples_SampleTypeId] ON [lab].[Samples] ([SampleTypeId]);
END;

IF NOT EXISTS (
    SELECT * FROM [core].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261109110026_AddSamples'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [UX_Samples_OneLivePerOrderType] ON [lab].[Samples] ([OrderId], [SampleTypeId]) WHERE [Status] <> 80 AND [Status] <> 90');
END;

IF NOT EXISTS (
    SELECT * FROM [core].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261109110026_AddSamples'
)
BEGIN
    CREATE INDEX [IX_SampleTests_OrderItemId] ON [lab].[SampleTests] ([OrderItemId]);
END;

IF NOT EXISTS (
    SELECT * FROM [core].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261109110026_AddSamples'
)
BEGIN
    CREATE INDEX [IX_SampleTests_OrganizationId_OrderItemId] ON [lab].[SampleTests] ([OrganizationId], [OrderItemId]);
END;

IF NOT EXISTS (
    SELECT * FROM [core].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261109110026_AddSamples'
)
BEGIN
    CREATE INDEX [IX_SampleTests_OrganizationId_SampleId] ON [lab].[SampleTests] ([OrganizationId], [SampleId]);
END;

IF NOT EXISTS (
    SELECT * FROM [core].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261109110026_AddSamples'
)
BEGIN
    CREATE INDEX [IX_SampleTransferItems_OrganizationId_SampleId] ON [lab].[SampleTransferItems] ([OrganizationId], [SampleId]);
END;

IF NOT EXISTS (
    SELECT * FROM [core].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261109110026_AddSamples'
)
BEGIN
    CREATE INDEX [IX_SampleTransferItems_OrganizationId_TransferId] ON [lab].[SampleTransferItems] ([OrganizationId], [TransferId]);
END;

IF NOT EXISTS (
    SELECT * FROM [core].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261109110026_AddSamples'
)
BEGIN
    CREATE INDEX [IX_SampleTransferItems_SampleId] ON [lab].[SampleTransferItems] ([SampleId]);
END;

IF NOT EXISTS (
    SELECT * FROM [core].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261109110026_AddSamples'
)
BEGIN
    CREATE INDEX [IX_SampleTransfers_OrganizationId_FromBranchId] ON [lab].[SampleTransfers] ([OrganizationId], [FromBranchId]);
END;

IF NOT EXISTS (
    SELECT * FROM [core].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261109110026_AddSamples'
)
BEGIN
    CREATE INDEX [IX_SampleTransfers_OrganizationId_Status_ToBranchId] ON [lab].[SampleTransfers] ([OrganizationId], [Status], [ToBranchId]);
END;

IF NOT EXISTS (
    SELECT * FROM [core].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261109110026_AddSamples'
)
BEGIN
    CREATE INDEX [IX_SampleTransfers_OrganizationId_ToBranchId] ON [lab].[SampleTransfers] ([OrganizationId], [ToBranchId]);
END;

IF NOT EXISTS (
    SELECT * FROM [core].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261109110026_AddSamples'
)
BEGIN
    CREATE UNIQUE INDEX [IX_SampleTransfers_OrganizationId_TransferNo] ON [lab].[SampleTransfers] ([OrganizationId], [TransferNo]);
END;

IF NOT EXISTS (
    SELECT * FROM [core].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261109110026_AddSamples'
)
BEGIN

    INSERT INTO lab.SampleRejectionReasons (OrganizationId, Code, NameEn, NameUr, RequiresNote, IsActive, SortOrder, CreatedAtUtc)
    SELECT o.Id, 'HAEMOLYSED', N'Haemolysed sample', N'ہیمولائزڈ نمونہ', 0, 1, 10, SYSUTCDATETIME()
      FROM core.Organizations o
     WHERE NOT EXISTS (SELECT 1 FROM lab.SampleRejectionReasons x WHERE x.OrganizationId = o.Id AND x.Code = 'HAEMOLYSED');
END;

IF NOT EXISTS (
    SELECT * FROM [core].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261109110026_AddSamples'
)
BEGIN

    INSERT INTO lab.SampleRejectionReasons (OrganizationId, Code, NameEn, NameUr, RequiresNote, IsActive, SortOrder, CreatedAtUtc)
    SELECT o.Id, 'CLOTTED', N'Clotted sample', N'جما ہوا نمونہ', 0, 1, 20, SYSUTCDATETIME()
      FROM core.Organizations o
     WHERE NOT EXISTS (SELECT 1 FROM lab.SampleRejectionReasons x WHERE x.OrganizationId = o.Id AND x.Code = 'CLOTTED');
END;

IF NOT EXISTS (
    SELECT * FROM [core].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261109110026_AddSamples'
)
BEGIN

    INSERT INTO lab.SampleRejectionReasons (OrganizationId, Code, NameEn, NameUr, RequiresNote, IsActive, SortOrder, CreatedAtUtc)
    SELECT o.Id, 'INSUFFICIENT', N'Insufficient volume', N'مقدار ناکافی', 0, 1, 30, SYSUTCDATETIME()
      FROM core.Organizations o
     WHERE NOT EXISTS (SELECT 1 FROM lab.SampleRejectionReasons x WHERE x.OrganizationId = o.Id AND x.Code = 'INSUFFICIENT');
END;

IF NOT EXISTS (
    SELECT * FROM [core].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261109110026_AddSamples'
)
BEGIN

    INSERT INTO lab.SampleRejectionReasons (OrganizationId, Code, NameEn, NameUr, RequiresNote, IsActive, SortOrder, CreatedAtUtc)
    SELECT o.Id, 'WRONG_CONTAINER', N'Wrong container or additive', N'غلط کنٹینر یا ایڈیٹو', 0, 1, 40, SYSUTCDATETIME()
      FROM core.Organizations o
     WHERE NOT EXISTS (SELECT 1 FROM lab.SampleRejectionReasons x WHERE x.OrganizationId = o.Id AND x.Code = 'WRONG_CONTAINER');
END;

IF NOT EXISTS (
    SELECT * FROM [core].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261109110026_AddSamples'
)
BEGIN

    INSERT INTO lab.SampleRejectionReasons (OrganizationId, Code, NameEn, NameUr, RequiresNote, IsActive, SortOrder, CreatedAtUtc)
    SELECT o.Id, 'MISLABELLED', N'Unlabelled or mislabelled', N'لیبل نہیں یا غلط لیبل', 1, 1, 50, SYSUTCDATETIME()
      FROM core.Organizations o
     WHERE NOT EXISTS (SELECT 1 FROM lab.SampleRejectionReasons x WHERE x.OrganizationId = o.Id AND x.Code = 'MISLABELLED');
END;

IF NOT EXISTS (
    SELECT * FROM [core].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261109110026_AddSamples'
)
BEGIN

    INSERT INTO lab.SampleRejectionReasons (OrganizationId, Code, NameEn, NameUr, RequiresNote, IsActive, SortOrder, CreatedAtUtc)
    SELECT o.Id, 'LEAKED', N'Leaked or broken container', N'رسنے والا یا ٹوٹا ہوا کنٹینر', 0, 1, 60, SYSUTCDATETIME()
      FROM core.Organizations o
     WHERE NOT EXISTS (SELECT 1 FROM lab.SampleRejectionReasons x WHERE x.OrganizationId = o.Id AND x.Code = 'LEAKED');
END;

IF NOT EXISTS (
    SELECT * FROM [core].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261109110026_AddSamples'
)
BEGIN

    INSERT INTO lab.SampleRejectionReasons (OrganizationId, Code, NameEn, NameUr, RequiresNote, IsActive, SortOrder, CreatedAtUtc)
    SELECT o.Id, 'DELAYED', N'Delayed transport', N'نقل و حمل میں تاخیر', 0, 1, 70, SYSUTCDATETIME()
      FROM core.Organizations o
     WHERE NOT EXISTS (SELECT 1 FROM lab.SampleRejectionReasons x WHERE x.OrganizationId = o.Id AND x.Code = 'DELAYED');
END;

IF NOT EXISTS (
    SELECT * FROM [core].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261109110026_AddSamples'
)
BEGIN

    INSERT INTO lab.SampleRejectionReasons (OrganizationId, Code, NameEn, NameUr, RequiresNote, IsActive, SortOrder, CreatedAtUtc)
    SELECT o.Id, 'CONTAMINATED', N'Contaminated sample', N'آلودہ نمونہ', 0, 1, 80, SYSUTCDATETIME()
      FROM core.Organizations o
     WHERE NOT EXISTS (SELECT 1 FROM lab.SampleRejectionReasons x WHERE x.OrganizationId = o.Id AND x.Code = 'CONTAMINATED');
END;

IF NOT EXISTS (
    SELECT * FROM [core].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261109110026_AddSamples'
)
BEGIN

    INSERT INTO lab.SampleRejectionReasons (OrganizationId, Code, NameEn, NameUr, RequiresNote, IsActive, SortOrder, CreatedAtUtc)
    SELECT o.Id, 'WRONG_PATIENT', N'Wrong patient or identity doubt', N'غلط مریض یا شناخت میں شک', 1, 1, 90, SYSUTCDATETIME()
      FROM core.Organizations o
     WHERE NOT EXISTS (SELECT 1 FROM lab.SampleRejectionReasons x WHERE x.OrganizationId = o.Id AND x.Code = 'WRONG_PATIENT');
END;

IF NOT EXISTS (
    SELECT * FROM [core].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261109110026_AddSamples'
)
BEGIN

    INSERT INTO lab.SampleRejectionReasons (OrganizationId, Code, NameEn, NameUr, RequiresNote, IsActive, SortOrder, CreatedAtUtc)
    SELECT o.Id, 'OTHER', N'Other (explain)', N'دیگر (وضاحت کریں)', 1, 1, 100, SYSUTCDATETIME()
      FROM core.Organizations o
     WHERE NOT EXISTS (SELECT 1 FROM lab.SampleRejectionReasons x WHERE x.OrganizationId = o.Id AND x.Code = 'OTHER');
END;

IF NOT EXISTS (
    SELECT * FROM [core].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261109110026_AddSamples'
)
BEGIN
    INSERT INTO [core].[__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261109110026_AddSamples', N'9.0.10');
END;

IF NOT EXISTS (
    SELECT * FROM [core].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261110061320_AddResults'
)
BEGIN
    CREATE TABLE [lab].[TestResults] (
        [Id] bigint NOT NULL IDENTITY,
        [OrganizationId] int NOT NULL,
        [OrderItemId] bigint NOT NULL,
        [OrderId] bigint NOT NULL,
        [SampleId] bigint NOT NULL,
        [PatientId] int NOT NULL,
        [OrderBranchId] int NOT NULL,
        [LabBranchId] int NOT NULL,
        [Version] int NOT NULL,
        [Status] int NOT NULL,
        [EnteredByUserId] int NULL,
        [SubmittedAtUtc] datetime2(3) NULL,
        [VerifiedByUserId] int NULL,
        [VerifiedAtUtc] datetime2(3) NULL,
        [ApprovedByUserId] int NULL,
        [ApprovedAtUtc] datetime2(3) NULL,
        [AmendmentReason] nvarchar(500) NULL,
        [ReplacesResultId] bigint NULL,
        [Comment] nvarchar(1000) NULL,
        [CreatedAtUtc] datetime2(3) NOT NULL,
        [CreatedByUserId] int NULL,
        [UpdatedAtUtc] datetime2(3) NULL,
        [UpdatedByUserId] int NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_TestResults] PRIMARY KEY ([Id]),
        CONSTRAINT [AK_TestResults_OrganizationId_Id] UNIQUE ([OrganizationId], [Id]),
        CONSTRAINT [CK_TestResults_Approved] CHECK ([Status] < 30 OR ([ApprovedByUserId] IS NOT NULL AND [ApprovedAtUtc] IS NOT NULL)),
        CONSTRAINT [CK_TestResults_DraftClean] CHECK ([Status] <> 0 OR ([VerifiedByUserId] IS NULL AND [ApprovedByUserId] IS NULL)),
        CONSTRAINT [CK_TestResults_Status] CHECK ([Status] IN (0,10,20,30,90)),
        CONSTRAINT [CK_TestResults_Submitted] CHECK ([Status] = 0 OR ([EnteredByUserId] IS NOT NULL AND [SubmittedAtUtc] IS NOT NULL)),
        CONSTRAINT [CK_TestResults_Verified] CHECK ([Status] < 20 OR ([VerifiedByUserId] IS NOT NULL AND [VerifiedAtUtc] IS NOT NULL)),
        CONSTRAINT [CK_TestResults_Version] CHECK ([Version] >= 1 AND ([Version] = 1 OR [AmendmentReason] IS NOT NULL)),
        CONSTRAINT [FK_TestResults_Branches_OrganizationId_LabBranchId] FOREIGN KEY ([OrganizationId], [LabBranchId]) REFERENCES [core].[Branches] ([OrganizationId], [Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_TestResults_Branches_OrganizationId_OrderBranchId] FOREIGN KEY ([OrganizationId], [OrderBranchId]) REFERENCES [core].[Branches] ([OrganizationId], [Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_TestResults_OrderItems_OrganizationId_OrderItemId] FOREIGN KEY ([OrganizationId], [OrderItemId]) REFERENCES [lab].[OrderItems] ([OrganizationId], [Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_TestResults_Orders_OrganizationId_OrderId] FOREIGN KEY ([OrganizationId], [OrderId]) REFERENCES [lab].[Orders] ([OrganizationId], [Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_TestResults_Patients_OrganizationId_PatientId] FOREIGN KEY ([OrganizationId], [PatientId]) REFERENCES [patient].[Patients] ([OrganizationId], [Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_TestResults_Samples_OrganizationId_SampleId] FOREIGN KEY ([OrganizationId], [SampleId]) REFERENCES [lab].[Samples] ([OrganizationId], [Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_TestResults_TestResults_OrganizationId_ReplacesResultId] FOREIGN KEY ([OrganizationId], [ReplacesResultId]) REFERENCES [lab].[TestResults] ([OrganizationId], [Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [core].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261110061320_AddResults'
)
BEGIN
    CREATE TABLE [lab].[Worklists] (
        [Id] bigint NOT NULL IDENTITY,
        [OrganizationId] int NOT NULL,
        [BranchId] int NOT NULL,
        [WorklistNo] varchar(30) NOT NULL,
        [DepartmentId] int NOT NULL,
        [Status] int NOT NULL,
        [Notes] nvarchar(500) NULL,
        [ClosedAtUtc] datetime2(3) NULL,
        [ClosedByUserId] int NULL,
        [CreatedAtUtc] datetime2(3) NOT NULL,
        [CreatedByUserId] int NULL,
        [UpdatedAtUtc] datetime2(3) NULL,
        [UpdatedByUserId] int NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_Worklists] PRIMARY KEY ([Id]),
        CONSTRAINT [AK_Worklists_OrganizationId_Id] UNIQUE ([OrganizationId], [Id]),
        CONSTRAINT [CK_Worklists_Closed] CHECK ([Status] = 0 OR [ClosedAtUtc] IS NOT NULL),
        CONSTRAINT [CK_Worklists_Status] CHECK ([Status] IN (0,1)),
        CONSTRAINT [FK_Worklists_Branches_OrganizationId_BranchId] FOREIGN KEY ([OrganizationId], [BranchId]) REFERENCES [core].[Branches] ([OrganizationId], [Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Worklists_Departments_OrganizationId_DepartmentId] FOREIGN KEY ([OrganizationId], [DepartmentId]) REFERENCES [core].[Departments] ([OrganizationId], [Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [core].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261110061320_AddResults'
)
BEGIN
    CREATE TABLE [lab].[CriticalValueAlerts] (
        [Id] bigint NOT NULL IDENTITY,
        [OrganizationId] int NOT NULL,
        [ResultId] bigint NOT NULL,
        [OrderItemId] bigint NOT NULL,
        [PatientId] int NOT NULL,
        [LabBranchId] int NOT NULL,
        [ParameterId] int NOT NULL,
        [ValueText] nvarchar(200) NOT NULL,
        [Limit] decimal(18,6) NULL,
        [IsHigh] bit NOT NULL,
        [Status] int NOT NULL,
        [NotifiedTo] nvarchar(200) NULL,
        [Method] int NULL,
        [ReadBackConfirmed] bit NOT NULL,
        [NotifiedByUserId] int NULL,
        [NotifiedAtUtc] datetime2(3) NULL,
        [Comment] nvarchar(500) NULL,
        [CreatedAtUtc] datetime2(3) NOT NULL,
        [CreatedByUserId] int NULL,
        [UpdatedAtUtc] datetime2(3) NULL,
        [UpdatedByUserId] int NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_CriticalValueAlerts] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_CriticalValueAlerts_Method] CHECK ([Method] IS NULL OR [Method] IN (1,2,3)),
        CONSTRAINT [CK_CriticalValueAlerts_Notified] CHECK ([Status] = 0 OR ([NotifiedTo] IS NOT NULL AND [Method] IS NOT NULL AND [NotifiedByUserId] IS NOT NULL AND [NotifiedAtUtc] IS NOT NULL AND [ReadBackConfirmed] = 1)),
        CONSTRAINT [CK_CriticalValueAlerts_Status] CHECK ([Status] IN (0,1)),
        CONSTRAINT [FK_CriticalValueAlerts_Branches_OrganizationId_LabBranchId] FOREIGN KEY ([OrganizationId], [LabBranchId]) REFERENCES [core].[Branches] ([OrganizationId], [Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_CriticalValueAlerts_Patients_OrganizationId_PatientId] FOREIGN KEY ([OrganizationId], [PatientId]) REFERENCES [patient].[Patients] ([OrganizationId], [Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_CriticalValueAlerts_TestResults_OrganizationId_ResultId] FOREIGN KEY ([OrganizationId], [ResultId]) REFERENCES [lab].[TestResults] ([OrganizationId], [Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [core].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261110061320_AddResults'
)
BEGIN
    CREATE TABLE [lab].[ResultApprovals] (
        [Id] bigint NOT NULL IDENTITY,
        [OrganizationId] int NOT NULL,
        [ResultId] bigint NOT NULL,
        [Step] varchar(20) NOT NULL,
        [UserId] int NOT NULL,
        [AtUtc] datetime2(3) NOT NULL,
        [Comment] nvarchar(500) NULL,
        CONSTRAINT [PK_ResultApprovals] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ResultApprovals_TestResults_OrganizationId_ResultId] FOREIGN KEY ([OrganizationId], [ResultId]) REFERENCES [lab].[TestResults] ([OrganizationId], [Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [core].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261110061320_AddResults'
)
BEGIN
    CREATE TABLE [lab].[TestResultValues] (
        [Id] bigint NOT NULL IDENTITY,
        [OrganizationId] int NOT NULL,
        [ResultId] bigint NOT NULL,
        [ParameterId] int NOT NULL,
        [ValueNumeric] decimal(18,6) NULL,
        [OptionId] int NULL,
        [ValueText] nvarchar(4000) NULL,
        [ValueTextUr] nvarchar(4000) NULL,
        [UnitText] nvarchar(50) NULL,
        [DecimalPlaces] int NOT NULL,
        [Flag] int NOT NULL,
        [RangeId] int NULL,
        [RangeLow] decimal(18,6) NULL,
        [RangeHigh] decimal(18,6) NULL,
        [RangeCriticalLow] decimal(18,6) NULL,
        [RangeCriticalHigh] decimal(18,6) NULL,
        [RangeText] nvarchar(200) NULL,
        CONSTRAINT [PK_TestResultValues] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_TestResultValues_Decimals] CHECK ([DecimalPlaces] BETWEEN 0 AND 6),
        CONSTRAINT [CK_TestResultValues_Flag] CHECK ([Flag] BETWEEN 0 AND 6),
        CONSTRAINT [CK_TestResultValues_OneKind] CHECK (([ValueNumeric] IS NOT NULL AND [OptionId] IS NULL AND [ValueText] IS NULL) OR ([OptionId] IS NOT NULL AND [ValueNumeric] IS NULL) OR ([ValueNumeric] IS NULL AND [OptionId] IS NULL AND [ValueText] IS NOT NULL)),
        CONSTRAINT [FK_TestResultValues_ParameterOptions_OptionId] FOREIGN KEY ([OptionId]) REFERENCES [catalog].[ParameterOptions] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_TestResultValues_ReferenceRanges_RangeId] FOREIGN KEY ([RangeId]) REFERENCES [catalog].[ReferenceRanges] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_TestResultValues_TestParameters_OrganizationId_ParameterId] FOREIGN KEY ([OrganizationId], [ParameterId]) REFERENCES [catalog].[TestParameters] ([OrganizationId], [Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_TestResultValues_TestResults_OrganizationId_ResultId] FOREIGN KEY ([OrganizationId], [ResultId]) REFERENCES [lab].[TestResults] ([OrganizationId], [Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [core].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261110061320_AddResults'
)
BEGIN
    CREATE TABLE [lab].[WorklistItems] (
        [WorklistId] bigint NOT NULL,
        [OrderItemId] bigint NOT NULL,
        [OrganizationId] int NOT NULL,
        [IsOpen] bit NOT NULL,
        CONSTRAINT [PK_WorklistItems] PRIMARY KEY ([WorklistId], [OrderItemId]),
        CONSTRAINT [FK_WorklistItems_OrderItems_OrganizationId_OrderItemId] FOREIGN KEY ([OrganizationId], [OrderItemId]) REFERENCES [lab].[OrderItems] ([OrganizationId], [Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_WorklistItems_Worklists_OrganizationId_WorklistId] FOREIGN KEY ([OrganizationId], [WorklistId]) REFERENCES [lab].[Worklists] ([OrganizationId], [Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [core].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261110061320_AddResults'
)
BEGIN
    CREATE INDEX [IX_CriticalValueAlerts_OrganizationId_LabBranchId] ON [lab].[CriticalValueAlerts] ([OrganizationId], [LabBranchId]);
END;

IF NOT EXISTS (
    SELECT * FROM [core].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261110061320_AddResults'
)
BEGIN
    CREATE INDEX [IX_CriticalValueAlerts_OrganizationId_PatientId] ON [lab].[CriticalValueAlerts] ([OrganizationId], [PatientId]);
END;

IF NOT EXISTS (
    SELECT * FROM [core].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261110061320_AddResults'
)
BEGIN
    CREATE INDEX [IX_CriticalValueAlerts_OrganizationId_ResultId] ON [lab].[CriticalValueAlerts] ([OrganizationId], [ResultId]);
END;

IF NOT EXISTS (
    SELECT * FROM [core].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261110061320_AddResults'
)
BEGIN
    CREATE INDEX [IX_CriticalValueAlerts_OrganizationId_Status_LabBranchId] ON [lab].[CriticalValueAlerts] ([OrganizationId], [Status], [LabBranchId]);
END;

IF NOT EXISTS (
    SELECT * FROM [core].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261110061320_AddResults'
)
BEGIN
    CREATE INDEX [IX_CriticalValueAlerts_ResultId_ParameterId] ON [lab].[CriticalValueAlerts] ([ResultId], [ParameterId]);
END;

IF NOT EXISTS (
    SELECT * FROM [core].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261110061320_AddResults'
)
BEGIN
    CREATE INDEX [IX_ResultApprovals_OrganizationId_ResultId] ON [lab].[ResultApprovals] ([OrganizationId], [ResultId]);
END;

IF NOT EXISTS (
    SELECT * FROM [core].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261110061320_AddResults'
)
BEGIN
    CREATE INDEX [IX_ResultApprovals_ResultId_Id] ON [lab].[ResultApprovals] ([ResultId], [Id]);
END;

IF NOT EXISTS (
    SELECT * FROM [core].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261110061320_AddResults'
)
BEGIN
    CREATE UNIQUE INDEX [IX_TestResults_OrderItemId_Version] ON [lab].[TestResults] ([OrderItemId], [Version]);
END;

IF NOT EXISTS (
    SELECT * FROM [core].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261110061320_AddResults'
)
BEGIN
    CREATE INDEX [IX_TestResults_OrganizationId_LabBranchId] ON [lab].[TestResults] ([OrganizationId], [LabBranchId]);
END;

IF NOT EXISTS (
    SELECT * FROM [core].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261110061320_AddResults'
)
BEGIN
    CREATE INDEX [IX_TestResults_OrganizationId_OrderBranchId] ON [lab].[TestResults] ([OrganizationId], [OrderBranchId]);
END;

IF NOT EXISTS (
    SELECT * FROM [core].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261110061320_AddResults'
)
BEGIN
    CREATE INDEX [IX_TestResults_OrganizationId_OrderId] ON [lab].[TestResults] ([OrganizationId], [OrderId]);
END;

IF NOT EXISTS (
    SELECT * FROM [core].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261110061320_AddResults'
)
BEGIN
    CREATE INDEX [IX_TestResults_OrganizationId_OrderItemId] ON [lab].[TestResults] ([OrganizationId], [OrderItemId]);
END;

IF NOT EXISTS (
    SELECT * FROM [core].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261110061320_AddResults'
)
BEGIN
    CREATE INDEX [IX_TestResults_OrganizationId_PatientId] ON [lab].[TestResults] ([OrganizationId], [PatientId]);
END;

IF NOT EXISTS (
    SELECT * FROM [core].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261110061320_AddResults'
)
BEGIN
    CREATE INDEX [IX_TestResults_OrganizationId_ReplacesResultId] ON [lab].[TestResults] ([OrganizationId], [ReplacesResultId]);
END;

IF NOT EXISTS (
    SELECT * FROM [core].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261110061320_AddResults'
)
BEGIN
    CREATE INDEX [IX_TestResults_OrganizationId_SampleId] ON [lab].[TestResults] ([OrganizationId], [SampleId]);
END;

IF NOT EXISTS (
    SELECT * FROM [core].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261110061320_AddResults'
)
BEGIN
    CREATE INDEX [IX_TestResults_OrganizationId_Status_LabBranchId] ON [lab].[TestResults] ([OrganizationId], [Status], [LabBranchId]);
END;

IF NOT EXISTS (
    SELECT * FROM [core].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261110061320_AddResults'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [UX_TestResults_OneInProgress] ON [lab].[TestResults] ([OrderItemId]) WHERE [Status] < 30');
END;

IF NOT EXISTS (
    SELECT * FROM [core].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261110061320_AddResults'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [UX_TestResults_OneReleased] ON [lab].[TestResults] ([OrderItemId]) WHERE [Status] = 30');
END;

IF NOT EXISTS (
    SELECT * FROM [core].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261110061320_AddResults'
)
BEGIN
    CREATE INDEX [IX_TestResultValues_OptionId] ON [lab].[TestResultValues] ([OptionId]);
END;

IF NOT EXISTS (
    SELECT * FROM [core].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261110061320_AddResults'
)
BEGIN
    CREATE INDEX [IX_TestResultValues_OrganizationId_ParameterId] ON [lab].[TestResultValues] ([OrganizationId], [ParameterId]);
END;

IF NOT EXISTS (
    SELECT * FROM [core].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261110061320_AddResults'
)
BEGIN
    CREATE INDEX [IX_TestResultValues_OrganizationId_ResultId] ON [lab].[TestResultValues] ([OrganizationId], [ResultId]);
END;

IF NOT EXISTS (
    SELECT * FROM [core].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261110061320_AddResults'
)
BEGIN
    CREATE INDEX [IX_TestResultValues_RangeId] ON [lab].[TestResultValues] ([RangeId]);
END;

IF NOT EXISTS (
    SELECT * FROM [core].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261110061320_AddResults'
)
BEGIN
    CREATE UNIQUE INDEX [IX_TestResultValues_ResultId_ParameterId] ON [lab].[TestResultValues] ([ResultId], [ParameterId]);
END;

IF NOT EXISTS (
    SELECT * FROM [core].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261110061320_AddResults'
)
BEGIN
    CREATE INDEX [IX_WorklistItems_OrganizationId_OrderItemId] ON [lab].[WorklistItems] ([OrganizationId], [OrderItemId]);
END;

IF NOT EXISTS (
    SELECT * FROM [core].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261110061320_AddResults'
)
BEGIN
    CREATE INDEX [IX_WorklistItems_OrganizationId_WorklistId] ON [lab].[WorklistItems] ([OrganizationId], [WorklistId]);
END;

IF NOT EXISTS (
    SELECT * FROM [core].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261110061320_AddResults'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [UX_WorklistItems_OneOpen] ON [lab].[WorklistItems] ([OrderItemId]) WHERE [IsOpen] = 1');
END;

IF NOT EXISTS (
    SELECT * FROM [core].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261110061320_AddResults'
)
BEGIN
    CREATE INDEX [IX_Worklists_OrganizationId_BranchId_Status] ON [lab].[Worklists] ([OrganizationId], [BranchId], [Status]);
END;

IF NOT EXISTS (
    SELECT * FROM [core].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261110061320_AddResults'
)
BEGIN
    CREATE INDEX [IX_Worklists_OrganizationId_DepartmentId] ON [lab].[Worklists] ([OrganizationId], [DepartmentId]);
END;

IF NOT EXISTS (
    SELECT * FROM [core].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261110061320_AddResults'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Worklists_OrganizationId_WorklistNo] ON [lab].[Worklists] ([OrganizationId], [WorklistNo]);
END;

IF NOT EXISTS (
    SELECT * FROM [core].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261110061320_AddResults'
)
BEGIN
    INSERT INTO [core].[__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261110061320_AddResults', N'9.0.10');
END;

IF NOT EXISTS (
    SELECT * FROM [core].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261110063645_AddReports'
)
BEGIN
    ALTER TABLE [security].[Users] ADD [RegistrationNo] nvarchar(50) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [core].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261110063645_AddReports'
)
BEGIN
    ALTER TABLE [security].[Users] ADD [SignatureContentType] varchar(50) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [core].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261110063645_AddReports'
)
BEGIN
    ALTER TABLE [security].[Users] ADD [SignatureImage] varbinary(max) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [core].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261110063645_AddReports'
)
BEGIN
    CREATE TABLE [lab].[LabReports] (
        [Id] bigint NOT NULL IDENTITY,
        [OrganizationId] int NOT NULL,
        [OrderId] bigint NOT NULL,
        [PatientId] int NOT NULL,
        [OrderBranchId] int NOT NULL,
        [LabBranchId] int NOT NULL,
        [ReportNo] varchar(30) NOT NULL,
        [Version] int NOT NULL,
        [Status] int NOT NULL,
        [Language] int NOT NULL,
        [IssuedByUserId] int NOT NULL,
        [IssuedAtUtc] datetime2(3) NOT NULL,
        [AmendmentReason] nvarchar(500) NULL,
        [ReplacesReportId] bigint NULL,
        [ReleasedCount] int NOT NULL,
        [ActiveTestCount] int NOT NULL,
        [LatestApprovedAtUtc] datetime2(3) NULL,
        [SnapshotJson] nvarchar(max) NOT NULL,
        [SnapshotHash] char(64) NOT NULL,
        [PrintCount] int NOT NULL,
        [LastPrintedAtUtc] datetime2(3) NULL,
        [CreatedAtUtc] datetime2(3) NOT NULL,
        [CreatedByUserId] int NULL,
        [UpdatedAtUtc] datetime2(3) NULL,
        [UpdatedByUserId] int NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_LabReports] PRIMARY KEY ([Id]),
        CONSTRAINT [AK_LabReports_OrganizationId_Id] UNIQUE ([OrganizationId], [Id]),
        CONSTRAINT [CK_LabReports_Counts] CHECK ([ReleasedCount] >= 1 AND [ActiveTestCount] >= [ReleasedCount] AND [PrintCount] >= 0),
        CONSTRAINT [CK_LabReports_Final] CHECK ([Status] <> 1 OR [ReleasedCount] = [ActiveTestCount]),
        CONSTRAINT [CK_LabReports_Hash] CHECK (LEN([SnapshotHash]) = 64 AND LEN([SnapshotJson]) > 2),
        CONSTRAINT [CK_LabReports_Language] CHECK ([Language] BETWEEN 0 AND 2),
        CONSTRAINT [CK_LabReports_Status] CHECK ([Status] IN (0,1,90)),
        CONSTRAINT [CK_LabReports_Version] CHECK ([Version] >= 1 AND ([Version] = 1 OR [AmendmentReason] IS NOT NULL)),
        CONSTRAINT [FK_LabReports_Branches_OrganizationId_LabBranchId] FOREIGN KEY ([OrganizationId], [LabBranchId]) REFERENCES [core].[Branches] ([OrganizationId], [Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_LabReports_Branches_OrganizationId_OrderBranchId] FOREIGN KEY ([OrganizationId], [OrderBranchId]) REFERENCES [core].[Branches] ([OrganizationId], [Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_LabReports_LabReports_OrganizationId_ReplacesReportId] FOREIGN KEY ([OrganizationId], [ReplacesReportId]) REFERENCES [lab].[LabReports] ([OrganizationId], [Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_LabReports_Orders_OrganizationId_OrderId] FOREIGN KEY ([OrganizationId], [OrderId]) REFERENCES [lab].[Orders] ([OrganizationId], [Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_LabReports_Patients_OrganizationId_PatientId] FOREIGN KEY ([OrganizationId], [PatientId]) REFERENCES [patient].[Patients] ([OrganizationId], [Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [core].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261110063645_AddReports'
)
BEGIN
    CREATE TABLE [lab].[ReportTemplates] (
        [Id] int NOT NULL IDENTITY,
        [OrganizationId] int NOT NULL,
        [LogoImage] varbinary(max) NULL,
        [LogoContentType] varchar(50) NULL,
        [CreatedAtUtc] datetime2(3) NOT NULL,
        [CreatedByUserId] int NULL,
        [UpdatedAtUtc] datetime2(3) NULL,
        [UpdatedByUserId] int NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_ReportTemplates] PRIMARY KEY ([Id]),
        CONSTRAINT [AK_ReportTemplates_OrganizationId_Id] UNIQUE ([OrganizationId], [Id]),
        CONSTRAINT [CK_ReportTemplates_Logo] CHECK ([LogoImage] IS NULL OR (DATALENGTH([LogoImage]) <= 307200 AND [LogoContentType] IN ('image/png','image/jpeg'))),
        CONSTRAINT [FK_ReportTemplates_Organizations_OrganizationId] FOREIGN KEY ([OrganizationId]) REFERENCES [core].[Organizations] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [core].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261110063645_AddReports'
)
BEGIN
    CREATE TABLE [lab].[ReportTemplateTranslations] (
        [Id] int NOT NULL IDENTITY,
        [OrganizationId] int NOT NULL,
        [TemplateId] int NOT NULL,
        [Language] varchar(2) NOT NULL,
        [HeaderNote] nvarchar(500) NULL,
        [FooterNote] nvarchar(500) NULL,
        [Disclaimer] nvarchar(1000) NULL,
        CONSTRAINT [PK_ReportTemplateTranslations] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_ReportTemplateTranslations_Language] CHECK ([Language] IN ('en','ur')),
        CONSTRAINT [FK_ReportTemplateTranslations_ReportTemplates_OrganizationId_TemplateId] FOREIGN KEY ([OrganizationId], [TemplateId]) REFERENCES [lab].[ReportTemplates] ([OrganizationId], [Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [core].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261110063645_AddReports'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'DescriptionEn', N'DescriptionUr', N'Key', N'Module') AND [object_id] = OBJECT_ID(N'[security].[Permissions]'))
        SET IDENTITY_INSERT [security].[Permissions] ON;
    EXEC(N'INSERT INTO [security].[Permissions] ([Id], [DescriptionEn], [DescriptionUr], [Key], [Module])
    VALUES (49, N''Issue (finalise) laboratory reports'', N''لیبارٹری رپورٹس جاری کریں'', ''reports.issue'', N''Reports''),
    (50, N''Edit the report template and logo'', N''رپورٹ ٹیمپلیٹ اور لوگو میں ترمیم'', ''reports.templates'', N''Reports'')');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'DescriptionEn', N'DescriptionUr', N'Key', N'Module') AND [object_id] = OBJECT_ID(N'[security].[Permissions]'))
        SET IDENTITY_INSERT [security].[Permissions] OFF;
END;

IF NOT EXISTS (
    SELECT * FROM [core].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261110063645_AddReports'
)
BEGIN
    EXEC(N'ALTER TABLE [security].[Users] ADD CONSTRAINT [CK_Users_Signature] CHECK ([SignatureImage] IS NULL OR (DATALENGTH([SignatureImage]) <= 204800 AND [SignatureContentType] IN (''image/png'',''image/jpeg'')))');
END;

IF NOT EXISTS (
    SELECT * FROM [core].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261110063645_AddReports'
)
BEGIN
    CREATE UNIQUE INDEX [IX_LabReports_OrderId_Version] ON [lab].[LabReports] ([OrderId], [Version]);
END;

IF NOT EXISTS (
    SELECT * FROM [core].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261110063645_AddReports'
)
BEGIN
    CREATE INDEX [IX_LabReports_OrganizationId_LabBranchId] ON [lab].[LabReports] ([OrganizationId], [LabBranchId]);
END;

IF NOT EXISTS (
    SELECT * FROM [core].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261110063645_AddReports'
)
BEGIN
    CREATE INDEX [IX_LabReports_OrganizationId_OrderBranchId] ON [lab].[LabReports] ([OrganizationId], [OrderBranchId]);
END;

IF NOT EXISTS (
    SELECT * FROM [core].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261110063645_AddReports'
)
BEGIN
    CREATE INDEX [IX_LabReports_OrganizationId_OrderId] ON [lab].[LabReports] ([OrganizationId], [OrderId]);
END;

IF NOT EXISTS (
    SELECT * FROM [core].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261110063645_AddReports'
)
BEGIN
    CREATE INDEX [IX_LabReports_OrganizationId_PatientId] ON [lab].[LabReports] ([OrganizationId], [PatientId]);
END;

IF NOT EXISTS (
    SELECT * FROM [core].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261110063645_AddReports'
)
BEGIN
    CREATE INDEX [IX_LabReports_OrganizationId_ReplacesReportId] ON [lab].[LabReports] ([OrganizationId], [ReplacesReportId]);
END;

IF NOT EXISTS (
    SELECT * FROM [core].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261110063645_AddReports'
)
BEGIN
    CREATE INDEX [IX_LabReports_OrganizationId_ReportNo] ON [lab].[LabReports] ([OrganizationId], [ReportNo]);
END;

IF NOT EXISTS (
    SELECT * FROM [core].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261110063645_AddReports'
)
BEGIN
    CREATE INDEX [IX_LabReports_OrganizationId_Status_LabBranchId] ON [lab].[LabReports] ([OrganizationId], [Status], [LabBranchId]);
END;

IF NOT EXISTS (
    SELECT * FROM [core].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261110063645_AddReports'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [UX_LabReports_OneLivePerOrder] ON [lab].[LabReports] ([OrderId]) WHERE [Status] <> 90');
END;

IF NOT EXISTS (
    SELECT * FROM [core].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261110063645_AddReports'
)
BEGIN
    CREATE UNIQUE INDEX [IX_ReportTemplates_OrganizationId] ON [lab].[ReportTemplates] ([OrganizationId]);
END;

IF NOT EXISTS (
    SELECT * FROM [core].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261110063645_AddReports'
)
BEGIN
    CREATE INDEX [IX_ReportTemplateTranslations_OrganizationId_TemplateId] ON [lab].[ReportTemplateTranslations] ([OrganizationId], [TemplateId]);
END;

IF NOT EXISTS (
    SELECT * FROM [core].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261110063645_AddReports'
)
BEGIN
    CREATE UNIQUE INDEX [IX_ReportTemplateTranslations_TemplateId_Language] ON [lab].[ReportTemplateTranslations] ([TemplateId], [Language]);
END;

IF NOT EXISTS (
    SELECT * FROM [core].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261110063645_AddReports'
)
BEGIN

    INSERT INTO security.RolePermissions (OrganizationId, RoleId, PermissionId)
    SELECT r.OrganizationId, r.Id, p.Id
      FROM security.Roles r
      JOIN security.Permissions p ON (p.[Key] = 'reports.issue' AND r.Code IN ('SYSADMIN','LABMGR','PATHOLOGIST'))
                                  OR (p.[Key] = 'reports.templates' AND r.Code IN ('SYSADMIN','LABMGR'))
     WHERE r.IsSystem = 1
       AND NOT EXISTS (SELECT 1 FROM security.RolePermissions x WHERE x.RoleId = r.Id AND x.PermissionId = p.Id);
END;

IF NOT EXISTS (
    SELECT * FROM [core].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261110063645_AddReports'
)
BEGIN
    INSERT INTO [core].[__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261110063645_AddReports', N'9.0.10');
END;

IF NOT EXISTS (
    SELECT * FROM [core].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261110073457_AddCorporateBilling'
)
BEGIN
    ALTER TABLE [billing].[Payments] DROP CONSTRAINT [CK_Payments_Method];
END;

IF NOT EXISTS (
    SELECT * FROM [core].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261110073457_AddCorporateBilling'
)
BEGIN
    ALTER TABLE [lab].[Orders] ADD [ContractId] int NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [core].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261110073457_AddCorporateBilling'
)
BEGIN
    ALTER TABLE [lab].[Orders] ADD [CorporateClientId] int NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [core].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261110073457_AddCorporateBilling'
)
BEGIN
    ALTER TABLE [billing].[Invoices] ADD [ContractId] int NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [core].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261110073457_AddCorporateBilling'
)
BEGIN
    ALTER TABLE [billing].[Invoices] ADD [CorporateClientId] int NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [core].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261110073457_AddCorporateBilling'
)
BEGIN
    ALTER TABLE [billing].[Invoices] ADD [DueDate] date NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [core].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261110073457_AddCorporateBilling'
)
BEGIN
    CREATE TABLE [billing].[CorporateClients] (
        [Id] int NOT NULL IDENTITY,
        [OrganizationId] int NOT NULL,
        [Code] varchar(20) NOT NULL,
        [NameEn] nvarchar(200) NOT NULL,
        [NameUr] nvarchar(200) NULL,
        [ContactName] nvarchar(200) NULL,
        [Phone] nvarchar(30) NULL,
        [Email] nvarchar(256) NULL,
        [AddressEn] nvarchar(300) NULL,
        [AddressUr] nvarchar(300) NULL,
        [CreditLimit] decimal(18,2) NULL,
        [PaymentTermsDays] int NOT NULL,
        [IsActive] bit NOT NULL,
        [CreatedAtUtc] datetime2(3) NOT NULL,
        [CreatedByUserId] int NULL,
        [UpdatedAtUtc] datetime2(3) NULL,
        [UpdatedByUserId] int NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_CorporateClients] PRIMARY KEY ([Id]),
        CONSTRAINT [AK_CorporateClients_OrganizationId_Id] UNIQUE ([OrganizationId], [Id]),
        CONSTRAINT [CK_CorporateClients_Credit] CHECK ([CreditLimit] IS NULL OR [CreditLimit] >= 0),
        CONSTRAINT [CK_CorporateClients_Terms] CHECK ([PaymentTermsDays] BETWEEN 0 AND 365)
    );
END;

IF NOT EXISTS (
    SELECT * FROM [core].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261110073457_AddCorporateBilling'
)
BEGIN
    CREATE TABLE [billing].[CorporateContracts] (
        [Id] int NOT NULL IDENTITY,
        [OrganizationId] int NOT NULL,
        [ClientId] int NOT NULL,
        [Name] nvarchar(200) NOT NULL,
        [ValidFrom] date NOT NULL,
        [ValidTo] date NULL,
        [DiscountPercent] decimal(5,2) NOT NULL,
        [IsActive] bit NOT NULL,
        [CreatedAtUtc] datetime2(3) NOT NULL,
        [CreatedByUserId] int NULL,
        [UpdatedAtUtc] datetime2(3) NULL,
        [UpdatedByUserId] int NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_CorporateContracts] PRIMARY KEY ([Id]),
        CONSTRAINT [AK_CorporateContracts_OrganizationId_Id] UNIQUE ([OrganizationId], [Id]),
        CONSTRAINT [CK_CorporateContracts_Dates] CHECK ([ValidTo] IS NULL OR [ValidTo] >= [ValidFrom]),
        CONSTRAINT [CK_CorporateContracts_Discount] CHECK ([DiscountPercent] >= 0 AND [DiscountPercent] <= 100),
        CONSTRAINT [FK_CorporateContracts_CorporateClients_OrganizationId_ClientId] FOREIGN KEY ([OrganizationId], [ClientId]) REFERENCES [billing].[CorporateClients] ([OrganizationId], [Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [core].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261110073457_AddCorporateBilling'
)
BEGIN
    CREATE TABLE [billing].[CorporateReceipts] (
        [Id] bigint NOT NULL IDENTITY,
        [OrganizationId] int NOT NULL,
        [ClientId] int NOT NULL,
        [ReceiptNo] varchar(30) NOT NULL,
        [IdempotencyKey] uniqueidentifier NOT NULL,
        [Kind] int NOT NULL,
        [Method] int NULL,
        [Amount] decimal(18,2) NOT NULL,
        [Allocated] decimal(18,2) NOT NULL,
        [Reference] nvarchar(100) NULL,
        [Reason] nvarchar(500) NULL,
        [ReceivedAtUtc] datetime2(3) NOT NULL,
        [ReceivedByUserId] int NOT NULL,
        [CreatedAtUtc] datetime2(3) NOT NULL,
        [CreatedByUserId] int NULL,
        [UpdatedAtUtc] datetime2(3) NULL,
        [UpdatedByUserId] int NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_CorporateReceipts] PRIMARY KEY ([Id]),
        CONSTRAINT [AK_CorporateReceipts_OrganizationId_Id] UNIQUE ([OrganizationId], [Id]),
        CONSTRAINT [CK_CorporateReceipts_Amount] CHECK ([Amount] > 0 AND [Allocated] >= 0 AND [Allocated] <= [Amount]),
        CONSTRAINT [CK_CorporateReceipts_Kind] CHECK (([Kind] = 1 AND [Method] BETWEEN 1 AND 5) OR ([Kind] = 2 AND [Method] IS NULL AND [Reason] IS NOT NULL)),
        CONSTRAINT [FK_CorporateReceipts_CorporateClients_OrganizationId_ClientId] FOREIGN KEY ([OrganizationId], [ClientId]) REFERENCES [billing].[CorporateClients] ([OrganizationId], [Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [core].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261110073457_AddCorporateBilling'
)
BEGIN
    CREATE TABLE [billing].[CorporateContractPrices] (
        [ContractId] int NOT NULL,
        [TestId] int NOT NULL,
        [OrganizationId] int NOT NULL,
        [Price] decimal(18,2) NOT NULL,
        CONSTRAINT [PK_CorporateContractPrices] PRIMARY KEY ([ContractId], [TestId]),
        CONSTRAINT [CK_CorporateContractPrices_Price] CHECK ([Price] >= 0),
        CONSTRAINT [FK_CorporateContractPrices_CorporateContracts_OrganizationId_ContractId] FOREIGN KEY ([OrganizationId], [ContractId]) REFERENCES [billing].[CorporateContracts] ([OrganizationId], [Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_CorporateContractPrices_Tests_OrganizationId_TestId] FOREIGN KEY ([OrganizationId], [TestId]) REFERENCES [catalog].[Tests] ([OrganizationId], [Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [core].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261110073457_AddCorporateBilling'
)
BEGIN
    CREATE TABLE [billing].[CorporateAllocations] (
        [Id] bigint NOT NULL IDENTITY,
        [OrganizationId] int NOT NULL,
        [ReceiptId] bigint NOT NULL,
        [InvoiceId] bigint NOT NULL,
        [PaymentId] bigint NOT NULL,
        [Amount] decimal(18,2) NOT NULL,
        [AllocatedAtUtc] datetime2(3) NOT NULL,
        [AllocatedByUserId] int NOT NULL,
        CONSTRAINT [PK_CorporateAllocations] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_CorporateAllocations_Amount] CHECK ([Amount] > 0),
        CONSTRAINT [FK_CorporateAllocations_CorporateReceipts_OrganizationId_ReceiptId] FOREIGN KEY ([OrganizationId], [ReceiptId]) REFERENCES [billing].[CorporateReceipts] ([OrganizationId], [Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_CorporateAllocations_Invoices_OrganizationId_InvoiceId] FOREIGN KEY ([OrganizationId], [InvoiceId]) REFERENCES [billing].[Invoices] ([OrganizationId], [Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_CorporateAllocations_Payments_PaymentId] FOREIGN KEY ([PaymentId]) REFERENCES [billing].[Payments] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [core].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261110073457_AddCorporateBilling'
)
BEGIN
    EXEC(N'ALTER TABLE [billing].[Payments] ADD CONSTRAINT [CK_Payments_Method] CHECK ([Method] BETWEEN 1 AND 6)');
END;

IF NOT EXISTS (
    SELECT * FROM [core].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261110073457_AddCorporateBilling'
)
BEGIN
    EXEC(N'CREATE INDEX [IX_Invoices_OrganizationId_CorporateClientId] ON [billing].[Invoices] ([OrganizationId], [CorporateClientId]) WHERE [CorporateClientId] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [core].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261110073457_AddCorporateBilling'
)
BEGIN
    CREATE INDEX [IX_CorporateAllocations_InvoiceId] ON [billing].[CorporateAllocations] ([InvoiceId]);
END;

IF NOT EXISTS (
    SELECT * FROM [core].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261110073457_AddCorporateBilling'
)
BEGIN
    CREATE INDEX [IX_CorporateAllocations_OrganizationId_InvoiceId] ON [billing].[CorporateAllocations] ([OrganizationId], [InvoiceId]);
END;

IF NOT EXISTS (
    SELECT * FROM [core].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261110073457_AddCorporateBilling'
)
BEGIN
    CREATE INDEX [IX_CorporateAllocations_OrganizationId_ReceiptId] ON [billing].[CorporateAllocations] ([OrganizationId], [ReceiptId]);
END;

IF NOT EXISTS (
    SELECT * FROM [core].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261110073457_AddCorporateBilling'
)
BEGIN
    CREATE UNIQUE INDEX [IX_CorporateAllocations_PaymentId] ON [billing].[CorporateAllocations] ([PaymentId]);
END;

IF NOT EXISTS (
    SELECT * FROM [core].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261110073457_AddCorporateBilling'
)
BEGIN
    CREATE INDEX [IX_CorporateAllocations_ReceiptId] ON [billing].[CorporateAllocations] ([ReceiptId]);
END;

IF NOT EXISTS (
    SELECT * FROM [core].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261110073457_AddCorporateBilling'
)
BEGIN
    CREATE UNIQUE INDEX [IX_CorporateClients_OrganizationId_Code] ON [billing].[CorporateClients] ([OrganizationId], [Code]);
END;

IF NOT EXISTS (
    SELECT * FROM [core].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261110073457_AddCorporateBilling'
)
BEGIN
    CREATE INDEX [IX_CorporateContractPrices_OrganizationId_ContractId] ON [billing].[CorporateContractPrices] ([OrganizationId], [ContractId]);
END;

IF NOT EXISTS (
    SELECT * FROM [core].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261110073457_AddCorporateBilling'
)
BEGIN
    CREATE INDEX [IX_CorporateContractPrices_OrganizationId_TestId] ON [billing].[CorporateContractPrices] ([OrganizationId], [TestId]);
END;

IF NOT EXISTS (
    SELECT * FROM [core].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261110073457_AddCorporateBilling'
)
BEGIN
    CREATE INDEX [IX_CorporateContracts_ClientId_IsActive] ON [billing].[CorporateContracts] ([ClientId], [IsActive]);
END;

IF NOT EXISTS (
    SELECT * FROM [core].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261110073457_AddCorporateBilling'
)
BEGIN
    CREATE INDEX [IX_CorporateContracts_OrganizationId_ClientId] ON [billing].[CorporateContracts] ([OrganizationId], [ClientId]);
END;

IF NOT EXISTS (
    SELECT * FROM [core].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261110073457_AddCorporateBilling'
)
BEGIN
    CREATE INDEX [IX_CorporateReceipts_ClientId_ReceivedAtUtc] ON [billing].[CorporateReceipts] ([ClientId], [ReceivedAtUtc]);
END;

IF NOT EXISTS (
    SELECT * FROM [core].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261110073457_AddCorporateBilling'
)
BEGIN
    CREATE INDEX [IX_CorporateReceipts_OrganizationId_ClientId] ON [billing].[CorporateReceipts] ([OrganizationId], [ClientId]);
END;

IF NOT EXISTS (
    SELECT * FROM [core].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261110073457_AddCorporateBilling'
)
BEGIN
    CREATE UNIQUE INDEX [IX_CorporateReceipts_OrganizationId_IdempotencyKey] ON [billing].[CorporateReceipts] ([OrganizationId], [IdempotencyKey]);
END;

IF NOT EXISTS (
    SELECT * FROM [core].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261110073457_AddCorporateBilling'
)
BEGIN
    CREATE UNIQUE INDEX [IX_CorporateReceipts_OrganizationId_ReceiptNo] ON [billing].[CorporateReceipts] ([OrganizationId], [ReceiptNo]);
END;

IF NOT EXISTS (
    SELECT * FROM [core].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261110073457_AddCorporateBilling'
)
BEGIN
    ALTER TABLE [billing].[Invoices] ADD CONSTRAINT [FK_Invoices_CorporateClients_OrganizationId_CorporateClientId] FOREIGN KEY ([OrganizationId], [CorporateClientId]) REFERENCES [billing].[CorporateClients] ([OrganizationId], [Id]) ON DELETE NO ACTION;
END;

IF NOT EXISTS (
    SELECT * FROM [core].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261110073457_AddCorporateBilling'
)
BEGIN

    INSERT INTO security.RolePermissions (OrganizationId, RoleId, PermissionId)
    SELECT r.OrganizationId, r.Id, p.Id
      FROM security.Roles r
      JOIN security.Permissions p ON p.[Key] IN ('orders.create','catalog.view')
     WHERE r.Code = 'CORPBILLING' AND r.IsSystem = 1
       AND NOT EXISTS (SELECT 1 FROM security.RolePermissions x WHERE x.RoleId = r.Id AND x.PermissionId = p.Id);
END;

IF NOT EXISTS (
    SELECT * FROM [core].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261110073457_AddCorporateBilling'
)
BEGIN
    INSERT INTO [core].[__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261110073457_AddCorporateBilling', N'9.0.10');
END;

IF NOT EXISTS (
    SELECT * FROM [core].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261110081027_AddInventory'
)
BEGIN
    IF SCHEMA_ID(N'inventory') IS NULL EXEC(N'CREATE SCHEMA [inventory];');
END;

IF NOT EXISTS (
    SELECT * FROM [core].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261110081027_AddInventory'
)
BEGIN
    CREATE TABLE [inventory].[Suppliers] (
        [Id] int NOT NULL IDENTITY,
        [OrganizationId] int NOT NULL,
        [Code] varchar(20) NOT NULL,
        [NameEn] nvarchar(200) NOT NULL,
        [NameUr] nvarchar(200) NULL,
        [ContactName] nvarchar(200) NULL,
        [Phone] nvarchar(30) NULL,
        [Email] nvarchar(256) NULL,
        [Address] nvarchar(300) NULL,
        [IsActive] bit NOT NULL,
        [CreatedAtUtc] datetime2(3) NOT NULL,
        [CreatedByUserId] int NULL,
        [UpdatedAtUtc] datetime2(3) NULL,
        [UpdatedByUserId] int NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_Suppliers] PRIMARY KEY ([Id]),
        CONSTRAINT [AK_Suppliers_OrganizationId_Id] UNIQUE ([OrganizationId], [Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [core].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261110081027_AddInventory'
)
BEGIN
    CREATE TABLE [inventory].[PurchaseReceipts] (
        [Id] bigint NOT NULL IDENTITY,
        [OrganizationId] int NOT NULL,
        [BranchId] int NOT NULL,
        [ReceiptNo] varchar(30) NOT NULL,
        [SupplierId] int NOT NULL,
        [SupplierInvoiceNo] nvarchar(60) NULL,
        [ReceivedAtUtc] datetime2(3) NOT NULL,
        [Notes] nvarchar(500) NULL,
        [IdempotencyKey] uniqueidentifier NOT NULL,
        [CreatedAtUtc] datetime2(3) NOT NULL,
        [CreatedByUserId] int NULL,
        [UpdatedAtUtc] datetime2(3) NULL,
        [UpdatedByUserId] int NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_PurchaseReceipts] PRIMARY KEY ([Id]),
        CONSTRAINT [AK_PurchaseReceipts_OrganizationId_Id] UNIQUE ([OrganizationId], [Id]),
        CONSTRAINT [FK_PurchaseReceipts_Branches_OrganizationId_BranchId] FOREIGN KEY ([OrganizationId], [BranchId]) REFERENCES [core].[Branches] ([OrganizationId], [Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_PurchaseReceipts_Suppliers_OrganizationId_SupplierId] FOREIGN KEY ([OrganizationId], [SupplierId]) REFERENCES [inventory].[Suppliers] ([OrganizationId], [Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [core].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261110081027_AddInventory'
)
BEGIN
    CREATE TABLE [inventory].[StockItems] (
        [Id] int NOT NULL IDENTITY,
        [OrganizationId] int NOT NULL,
        [Code] varchar(30) NOT NULL,
        [NameEn] nvarchar(200) NOT NULL,
        [NameUr] nvarchar(200) NULL,
        [Kind] int NOT NULL,
        [UnitName] nvarchar(30) NOT NULL,
        [ReorderLevel] decimal(18,3) NOT NULL,
        [TracksExpiry] bit NOT NULL,
        [DefaultSupplierId] int NULL,
        [IsActive] bit NOT NULL,
        [CreatedAtUtc] datetime2(3) NOT NULL,
        [CreatedByUserId] int NULL,
        [UpdatedAtUtc] datetime2(3) NULL,
        [UpdatedByUserId] int NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_StockItems] PRIMARY KEY ([Id]),
        CONSTRAINT [AK_StockItems_OrganizationId_Id] UNIQUE ([OrganizationId], [Id]),
        CONSTRAINT [CK_StockItems_Kind] CHECK ([Kind] IN (1,2)),
        CONSTRAINT [CK_StockItems_Reorder] CHECK ([ReorderLevel] >= 0),
        CONSTRAINT [FK_StockItems_Suppliers_OrganizationId_DefaultSupplierId] FOREIGN KEY ([OrganizationId], [DefaultSupplierId]) REFERENCES [inventory].[Suppliers] ([OrganizationId], [Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [core].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261110081027_AddInventory'
)
BEGIN
    CREATE TABLE [inventory].[ConsumptionProfileLines] (
        [TestId] int NOT NULL,
        [ItemId] int NOT NULL,
        [OrganizationId] int NOT NULL,
        [QuantityPerTest] decimal(18,3) NOT NULL,
        CONSTRAINT [PK_ConsumptionProfileLines] PRIMARY KEY ([TestId], [ItemId]),
        CONSTRAINT [CK_ConsumptionProfileLines_Quantity] CHECK ([QuantityPerTest] > 0),
        CONSTRAINT [FK_ConsumptionProfileLines_StockItems_OrganizationId_ItemId] FOREIGN KEY ([OrganizationId], [ItemId]) REFERENCES [inventory].[StockItems] ([OrganizationId], [Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_ConsumptionProfileLines_Tests_OrganizationId_TestId] FOREIGN KEY ([OrganizationId], [TestId]) REFERENCES [catalog].[Tests] ([OrganizationId], [Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [core].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261110081027_AddInventory'
)
BEGIN
    CREATE TABLE [inventory].[StockBatches] (
        [Id] bigint NOT NULL IDENTITY,
        [OrganizationId] int NOT NULL,
        [BranchId] int NOT NULL,
        [ItemId] int NOT NULL,
        [BatchNo] nvarchar(60) NOT NULL,
        [ExpiryDate] date NULL,
        [QuantityOnHand] decimal(18,3) NOT NULL,
        [UnitCost] decimal(18,4) NULL,
        [SupplierId] int NULL,
        [CreatedAtUtc] datetime2(3) NOT NULL,
        [CreatedByUserId] int NULL,
        [UpdatedAtUtc] datetime2(3) NULL,
        [UpdatedByUserId] int NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_StockBatches] PRIMARY KEY ([Id]),
        CONSTRAINT [AK_StockBatches_OrganizationId_Id] UNIQUE ([OrganizationId], [Id]),
        CONSTRAINT [CK_StockBatches_NotNegative] CHECK ([QuantityOnHand] >= 0 OR [BatchNo] = 'UNBATCHED'),
        CONSTRAINT [FK_StockBatches_Branches_OrganizationId_BranchId] FOREIGN KEY ([OrganizationId], [BranchId]) REFERENCES [core].[Branches] ([OrganizationId], [Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_StockBatches_StockItems_OrganizationId_ItemId] FOREIGN KEY ([OrganizationId], [ItemId]) REFERENCES [inventory].[StockItems] ([OrganizationId], [Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_StockBatches_Suppliers_OrganizationId_SupplierId] FOREIGN KEY ([OrganizationId], [SupplierId]) REFERENCES [inventory].[Suppliers] ([OrganizationId], [Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [core].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261110081027_AddInventory'
)
BEGIN
    CREATE TABLE [inventory].[StockShortfalls] (
        [Id] bigint NOT NULL IDENTITY,
        [OrganizationId] int NOT NULL,
        [BranchId] int NOT NULL,
        [ItemId] int NOT NULL,
        [Quantity] decimal(18,3) NOT NULL,
        [Reference] nvarchar(60) NULL,
        [AtUtc] datetime2(3) NOT NULL,
        [ResolvedAtUtc] datetime2(3) NULL,
        [ResolvedByUserId] int NULL,
        [ResolutionNote] nvarchar(300) NULL,
        CONSTRAINT [PK_StockShortfalls] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_StockShortfalls_Quantity] CHECK ([Quantity] > 0),
        CONSTRAINT [FK_StockShortfalls_Branches_OrganizationId_BranchId] FOREIGN KEY ([OrganizationId], [BranchId]) REFERENCES [core].[Branches] ([OrganizationId], [Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_StockShortfalls_StockItems_OrganizationId_ItemId] FOREIGN KEY ([OrganizationId], [ItemId]) REFERENCES [inventory].[StockItems] ([OrganizationId], [Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [core].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261110081027_AddInventory'
)
BEGIN
    CREATE TABLE [inventory].[PurchaseLines] (
        [Id] bigint NOT NULL IDENTITY,
        [OrganizationId] int NOT NULL,
        [ReceiptId] bigint NOT NULL,
        [ItemId] int NOT NULL,
        [BatchNo] nvarchar(60) NOT NULL,
        [ExpiryDate] date NULL,
        [Quantity] decimal(18,3) NOT NULL,
        [UnitCost] decimal(18,4) NULL,
        [BatchId] bigint NOT NULL,
        CONSTRAINT [PK_PurchaseLines] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_PurchaseLines_Quantity] CHECK ([Quantity] > 0 AND ([UnitCost] IS NULL OR [UnitCost] >= 0)),
        CONSTRAINT [FK_PurchaseLines_PurchaseReceipts_OrganizationId_ReceiptId] FOREIGN KEY ([OrganizationId], [ReceiptId]) REFERENCES [inventory].[PurchaseReceipts] ([OrganizationId], [Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_PurchaseLines_StockBatches_OrganizationId_BatchId] FOREIGN KEY ([OrganizationId], [BatchId]) REFERENCES [inventory].[StockBatches] ([OrganizationId], [Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_PurchaseLines_StockItems_OrganizationId_ItemId] FOREIGN KEY ([OrganizationId], [ItemId]) REFERENCES [inventory].[StockItems] ([OrganizationId], [Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [core].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261110081027_AddInventory'
)
BEGIN
    CREATE TABLE [inventory].[StockMovements] (
        [Id] bigint NOT NULL IDENTITY,
        [OrganizationId] int NOT NULL,
        [BranchId] int NOT NULL,
        [ItemId] int NOT NULL,
        [BatchId] bigint NOT NULL,
        [Type] int NOT NULL,
        [Quantity] decimal(18,3) NOT NULL,
        [BalanceAfter] decimal(18,3) NOT NULL,
        [Reason] nvarchar(300) NULL,
        [Reference] nvarchar(60) NULL,
        [ConsumptionKey] varchar(60) NULL,
        [PairedMovementId] bigint NULL,
        [UserId] int NOT NULL,
        [AtUtc] datetime2(3) NOT NULL,
        CONSTRAINT [PK_StockMovements] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_StockMovements_Direction] CHECK (([Type] IN (1,4,8) AND [Quantity] > 0) OR ([Type] IN (2,3,5,6,7) AND [Quantity] < 0)),
        CONSTRAINT [CK_StockMovements_Quantity] CHECK ([Quantity] <> 0),
        CONSTRAINT [CK_StockMovements_Type] CHECK ([Type] BETWEEN 1 AND 8),
        CONSTRAINT [FK_StockMovements_Branches_OrganizationId_BranchId] FOREIGN KEY ([OrganizationId], [BranchId]) REFERENCES [core].[Branches] ([OrganizationId], [Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_StockMovements_StockBatches_OrganizationId_BatchId] FOREIGN KEY ([OrganizationId], [BatchId]) REFERENCES [inventory].[StockBatches] ([OrganizationId], [Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_StockMovements_StockItems_OrganizationId_ItemId] FOREIGN KEY ([OrganizationId], [ItemId]) REFERENCES [inventory].[StockItems] ([OrganizationId], [Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [core].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261110081027_AddInventory'
)
BEGIN
    CREATE INDEX [IX_ConsumptionProfileLines_OrganizationId_ItemId] ON [inventory].[ConsumptionProfileLines] ([OrganizationId], [ItemId]);
END;

IF NOT EXISTS (
    SELECT * FROM [core].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261110081027_AddInventory'
)
BEGIN
    CREATE INDEX [IX_ConsumptionProfileLines_OrganizationId_TestId] ON [inventory].[ConsumptionProfileLines] ([OrganizationId], [TestId]);
END;

IF NOT EXISTS (
    SELECT * FROM [core].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261110081027_AddInventory'
)
BEGIN
    CREATE INDEX [IX_PurchaseLines_OrganizationId_BatchId] ON [inventory].[PurchaseLines] ([OrganizationId], [BatchId]);
END;

IF NOT EXISTS (
    SELECT * FROM [core].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261110081027_AddInventory'
)
BEGIN
    CREATE INDEX [IX_PurchaseLines_OrganizationId_ItemId] ON [inventory].[PurchaseLines] ([OrganizationId], [ItemId]);
END;

IF NOT EXISTS (
    SELECT * FROM [core].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261110081027_AddInventory'
)
BEGIN
    CREATE INDEX [IX_PurchaseLines_OrganizationId_ReceiptId] ON [inventory].[PurchaseLines] ([OrganizationId], [ReceiptId]);
END;

IF NOT EXISTS (
    SELECT * FROM [core].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261110081027_AddInventory'
)
BEGIN
    CREATE INDEX [IX_PurchaseReceipts_OrganizationId_BranchId] ON [inventory].[PurchaseReceipts] ([OrganizationId], [BranchId]);
END;

IF NOT EXISTS (
    SELECT * FROM [core].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261110081027_AddInventory'
)
BEGIN
    CREATE UNIQUE INDEX [IX_PurchaseReceipts_OrganizationId_IdempotencyKey] ON [inventory].[PurchaseReceipts] ([OrganizationId], [IdempotencyKey]);
END;

IF NOT EXISTS (
    SELECT * FROM [core].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261110081027_AddInventory'
)
BEGIN
    CREATE UNIQUE INDEX [IX_PurchaseReceipts_OrganizationId_ReceiptNo] ON [inventory].[PurchaseReceipts] ([OrganizationId], [ReceiptNo]);
END;

IF NOT EXISTS (
    SELECT * FROM [core].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261110081027_AddInventory'
)
BEGIN
    CREATE INDEX [IX_PurchaseReceipts_OrganizationId_SupplierId] ON [inventory].[PurchaseReceipts] ([OrganizationId], [SupplierId]);
END;

IF NOT EXISTS (
    SELECT * FROM [core].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261110081027_AddInventory'
)
BEGIN
    CREATE UNIQUE INDEX [IX_StockBatches_ItemId_BranchId_BatchNo] ON [inventory].[StockBatches] ([ItemId], [BranchId], [BatchNo]);
END;

IF NOT EXISTS (
    SELECT * FROM [core].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261110081027_AddInventory'
)
BEGIN
    CREATE INDEX [IX_StockBatches_OrganizationId_BranchId_ExpiryDate] ON [inventory].[StockBatches] ([OrganizationId], [BranchId], [ExpiryDate]);
END;

IF NOT EXISTS (
    SELECT * FROM [core].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261110081027_AddInventory'
)
BEGIN
    CREATE INDEX [IX_StockBatches_OrganizationId_ItemId] ON [inventory].[StockBatches] ([OrganizationId], [ItemId]);
END;

IF NOT EXISTS (
    SELECT * FROM [core].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261110081027_AddInventory'
)
BEGIN
    CREATE INDEX [IX_StockBatches_OrganizationId_SupplierId] ON [inventory].[StockBatches] ([OrganizationId], [SupplierId]);
END;

IF NOT EXISTS (
    SELECT * FROM [core].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261110081027_AddInventory'
)
BEGIN
    CREATE UNIQUE INDEX [IX_StockItems_OrganizationId_Code] ON [inventory].[StockItems] ([OrganizationId], [Code]);
END;

IF NOT EXISTS (
    SELECT * FROM [core].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261110081027_AddInventory'
)
BEGIN
    CREATE INDEX [IX_StockItems_OrganizationId_DefaultSupplierId] ON [inventory].[StockItems] ([OrganizationId], [DefaultSupplierId]);
END;

IF NOT EXISTS (
    SELECT * FROM [core].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261110081027_AddInventory'
)
BEGIN
    CREATE INDEX [IX_StockMovements_ItemId_AtUtc] ON [inventory].[StockMovements] ([ItemId], [AtUtc]);
END;

IF NOT EXISTS (
    SELECT * FROM [core].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261110081027_AddInventory'
)
BEGIN
    CREATE INDEX [IX_StockMovements_OrganizationId_BatchId] ON [inventory].[StockMovements] ([OrganizationId], [BatchId]);
END;

IF NOT EXISTS (
    SELECT * FROM [core].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261110081027_AddInventory'
)
BEGIN
    CREATE INDEX [IX_StockMovements_OrganizationId_BranchId_AtUtc] ON [inventory].[StockMovements] ([OrganizationId], [BranchId], [AtUtc]);
END;

IF NOT EXISTS (
    SELECT * FROM [core].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261110081027_AddInventory'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_StockMovements_OrganizationId_ConsumptionKey] ON [inventory].[StockMovements] ([OrganizationId], [ConsumptionKey]) WHERE [ConsumptionKey] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [core].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261110081027_AddInventory'
)
BEGIN
    CREATE INDEX [IX_StockMovements_OrganizationId_ItemId] ON [inventory].[StockMovements] ([OrganizationId], [ItemId]);
END;

IF NOT EXISTS (
    SELECT * FROM [core].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261110081027_AddInventory'
)
BEGIN
    CREATE INDEX [IX_StockShortfalls_OrganizationId_BranchId_ResolvedAtUtc] ON [inventory].[StockShortfalls] ([OrganizationId], [BranchId], [ResolvedAtUtc]);
END;

IF NOT EXISTS (
    SELECT * FROM [core].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261110081027_AddInventory'
)
BEGIN
    CREATE INDEX [IX_StockShortfalls_OrganizationId_ItemId] ON [inventory].[StockShortfalls] ([OrganizationId], [ItemId]);
END;

IF NOT EXISTS (
    SELECT * FROM [core].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261110081027_AddInventory'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Suppliers_OrganizationId_Code] ON [inventory].[Suppliers] ([OrganizationId], [Code]);
END;

IF NOT EXISTS (
    SELECT * FROM [core].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261110081027_AddInventory'
)
BEGIN
    INSERT INTO [core].[__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261110081027_AddInventory', N'9.0.10');
END;

IF NOT EXISTS (
    SELECT * FROM [core].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261110154051_AddPerformanceIndexes'
)
BEGIN
    CREATE INDEX [IX_TestResults_OrderId_Status] ON [lab].[TestResults] ([OrderId], [Status]) INCLUDE ([OrganizationId]);
END;

IF NOT EXISTS (
    SELECT * FROM [core].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261110154051_AddPerformanceIndexes'
)
BEGIN
    CREATE INDEX [IX_TestResults_Status_ApprovedAtUtc_Id] ON [lab].[TestResults] ([Status], [ApprovedAtUtc] DESC, [Id] DESC) INCLUDE ([OrganizationId], [OrderId]);
END;

IF NOT EXISTS (
    SELECT * FROM [core].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261110154051_AddPerformanceIndexes'
)
BEGIN
    CREATE INDEX [IX_TestResults_Status_VerifiedAtUtc] ON [lab].[TestResults] ([Status], [VerifiedAtUtc]) INCLUDE ([OrganizationId]);
END;

IF NOT EXISTS (
    SELECT * FROM [core].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261110154051_AddPerformanceIndexes'
)
BEGIN
    CREATE INDEX [IX_Samples_Barcode] ON [lab].[Samples] ([Barcode]) INCLUDE ([OrganizationId], [OriginBranchId], [CurrentBranchId], [DestinationBranchId]);
END;

IF NOT EXISTS (
    SELECT * FROM [core].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261110154051_AddPerformanceIndexes'
)
BEGIN
    CREATE INDEX [IX_Samples_PatientId] ON [lab].[Samples] ([PatientId]) INCLUDE ([OrganizationId], [OriginBranchId], [CurrentBranchId], [DestinationBranchId]);
END;

IF NOT EXISTS (
    SELECT * FROM [core].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261110154051_AddPerformanceIndexes'
)
BEGIN
    EXEC(N'CREATE INDEX [IX_Samples_ReplacesSampleId] ON [lab].[Samples] ([ReplacesSampleId]) INCLUDE ([OrganizationId], [OriginBranchId], [CurrentBranchId], [DestinationBranchId]) WHERE [ReplacesSampleId] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [core].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261110154051_AddPerformanceIndexes'
)
BEGIN
    CREATE INDEX [IX_Samples_Status_ReceivedAtUtc] ON [lab].[Samples] ([Status], [ReceivedAtUtc] DESC) INCLUDE ([OrganizationId], [OriginBranchId], [CurrentBranchId], [DestinationBranchId]);
END;

IF NOT EXISTS (
    SELECT * FROM [core].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261110154051_AddPerformanceIndexes'
)
BEGIN
    CREATE INDEX [IX_Payments_ReceivedAtUtc] ON [billing].[Payments] ([ReceivedAtUtc] DESC) INCLUDE ([OrganizationId], [BranchId], [Method], [Amount], [InvoiceId]);
END;

IF NOT EXISTS (
    SELECT * FROM [core].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261110154051_AddPerformanceIndexes'
)
BEGIN
    EXEC(N'CREATE INDEX [IX_Patients_Cnic] ON [patient].[Patients] ([Cnic]) INCLUDE ([OrganizationId], [IsDeleted]) WHERE [Cnic] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [core].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261110154051_AddPerformanceIndexes'
)
BEGIN
    CREATE INDEX [IX_Patients_CreatedAtUtc] ON [patient].[Patients] ([CreatedAtUtc] DESC) INCLUDE ([OrganizationId], [IsDeleted]);
END;

IF NOT EXISTS (
    SELECT * FROM [core].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261110154051_AddPerformanceIndexes'
)
BEGIN
    EXEC(N'CREATE INDEX [IX_Patients_Mobile] ON [patient].[Patients] ([Mobile]) INCLUDE ([OrganizationId], [IsDeleted]) WHERE [Mobile] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [core].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261110154051_AddPerformanceIndexes'
)
BEGIN
    CREATE INDEX [IX_Patients_PatientNo] ON [patient].[Patients] ([PatientNo]) INCLUDE ([OrganizationId], [IsDeleted]);
END;

IF NOT EXISTS (
    SELECT * FROM [core].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261110154051_AddPerformanceIndexes'
)
BEGIN
    CREATE INDEX [IX_Orders_CreatedAtUtc] ON [lab].[Orders] ([CreatedAtUtc]) INCLUDE ([OrganizationId], [BranchId], [Status]);
END;

IF NOT EXISTS (
    SELECT * FROM [core].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261110154051_AddPerformanceIndexes'
)
BEGIN
    CREATE INDEX [IX_Orders_OrderNo] ON [lab].[Orders] ([OrderNo]) INCLUDE ([OrganizationId], [BranchId]);
END;

IF NOT EXISTS (
    SELECT * FROM [core].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261110154051_AddPerformanceIndexes'
)
BEGIN
    CREATE INDEX [IX_Orders_PatientId] ON [lab].[Orders] ([PatientId]) INCLUDE ([OrganizationId], [BranchId]);
END;

IF NOT EXISTS (
    SELECT * FROM [core].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261110154051_AddPerformanceIndexes'
)
BEGIN
    CREATE INDEX [IX_LabReports_IssuedAtUtc_Id] ON [lab].[LabReports] ([IssuedAtUtc] DESC, [Id] DESC) INCLUDE ([OrganizationId], [Status]);
END;

IF NOT EXISTS (
    SELECT * FROM [core].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261110154051_AddPerformanceIndexes'
)
BEGIN
    CREATE INDEX [IX_LabReports_PatientId] ON [lab].[LabReports] ([PatientId]) INCLUDE ([OrganizationId], [Status]);
END;

IF NOT EXISTS (
    SELECT * FROM [core].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261110154051_AddPerformanceIndexes'
)
BEGIN
    CREATE INDEX [IX_LabReports_ReportNo] ON [lab].[LabReports] ([ReportNo]) INCLUDE ([OrganizationId], [Status]);
END;

IF NOT EXISTS (
    SELECT * FROM [core].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261110154051_AddPerformanceIndexes'
)
BEGIN
    CREATE INDEX [IX_Invoices_InvoiceNo] ON [billing].[Invoices] ([InvoiceNo]) INCLUDE ([OrganizationId], [BranchId], [OrderId]);
END;

IF NOT EXISTS (
    SELECT * FROM [core].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261110154051_AddPerformanceIndexes'
)
BEGIN
    CREATE INDEX [IX_Invoices_OrderId] ON [billing].[Invoices] ([OrderId]) INCLUDE ([OrganizationId], [BranchId]);
END;

IF NOT EXISTS (
    SELECT * FROM [core].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261110154051_AddPerformanceIndexes'
)
BEGIN
    CREATE INDEX [IX_AuditLogs_Org_EntityType_Action_AtUtc] ON [audit].[AuditLogs] ([OrganizationId], [EntityType], [Action], [AtUtc] DESC);
END;

IF NOT EXISTS (
    SELECT * FROM [core].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261110154051_AddPerformanceIndexes'
)
BEGIN
    INSERT INTO [core].[__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261110154051_AddPerformanceIndexes', N'9.0.10');
END;

IF NOT EXISTS (
    SELECT * FROM [core].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261111183315_AddReportLayouts'
)
BEGIN
    CREATE TABLE [lab].[ReportLayouts] (
        [Id] int NOT NULL IDENTITY,
        [OrganizationId] int NOT NULL,
        [Name] nvarchar(100) NOT NULL,
        [IsDefault] bit NOT NULL,
        [DesignJson] nvarchar(4000) NOT NULL,
        [CreatedAtUtc] datetime2(3) NOT NULL,
        [CreatedByUserId] int NULL,
        [UpdatedAtUtc] datetime2(3) NULL,
        [UpdatedByUserId] int NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_ReportLayouts] PRIMARY KEY ([Id]),
        CONSTRAINT [AK_ReportLayouts_OrganizationId_Id] UNIQUE ([OrganizationId], [Id]),
        CONSTRAINT [CK_ReportLayouts_Design] CHECK (LEN([DesignJson]) >= 2 AND ISJSON([DesignJson]) = 1),
        CONSTRAINT [FK_ReportLayouts_Organizations_OrganizationId] FOREIGN KEY ([OrganizationId]) REFERENCES [core].[Organizations] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [core].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261111183315_AddReportLayouts'
)
BEGIN
    CREATE UNIQUE INDEX [UX_ReportLayouts_Name] ON [lab].[ReportLayouts] ([OrganizationId], [Name]);
END;

IF NOT EXISTS (
    SELECT * FROM [core].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261111183315_AddReportLayouts'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [UX_ReportLayouts_OneDefault] ON [lab].[ReportLayouts] ([OrganizationId]) WHERE [IsDefault] = 1');
END;

IF NOT EXISTS (
    SELECT * FROM [core].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261111183315_AddReportLayouts'
)
BEGIN
    INSERT INTO [core].[__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261111183315_AddReportLayouts', N'9.0.10');
END;

IF NOT EXISTS (
    SELECT * FROM [core].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261111193546_AddReportBannerImages'
)
BEGIN
    ALTER TABLE [lab].[ReportTemplates] ADD [FooterImage] varbinary(max) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [core].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261111193546_AddReportBannerImages'
)
BEGIN
    ALTER TABLE [lab].[ReportTemplates] ADD [FooterImageContentType] varchar(50) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [core].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261111193546_AddReportBannerImages'
)
BEGIN
    ALTER TABLE [lab].[ReportTemplates] ADD [HeaderImage] varbinary(max) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [core].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261111193546_AddReportBannerImages'
)
BEGIN
    ALTER TABLE [lab].[ReportTemplates] ADD [HeaderImageContentType] varchar(50) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [core].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261111193546_AddReportBannerImages'
)
BEGIN
    EXEC(N'ALTER TABLE [lab].[ReportTemplates] ADD CONSTRAINT [CK_ReportTemplates_FooterImage] CHECK ([FooterImage] IS NULL OR (DATALENGTH([FooterImage]) <= 614400 AND [FooterImageContentType] IN (''image/png'',''image/jpeg'')))');
END;

IF NOT EXISTS (
    SELECT * FROM [core].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261111193546_AddReportBannerImages'
)
BEGIN
    EXEC(N'ALTER TABLE [lab].[ReportTemplates] ADD CONSTRAINT [CK_ReportTemplates_HeaderImage] CHECK ([HeaderImage] IS NULL OR (DATALENGTH([HeaderImage]) <= 614400 AND [HeaderImageContentType] IN (''image/png'',''image/jpeg'')))');
END;

IF NOT EXISTS (
    SELECT * FROM [core].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261111193546_AddReportBannerImages'
)
BEGIN
    INSERT INTO [core].[__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261111193546_AddReportBannerImages', N'9.0.10');
END;

IF NOT EXISTS (
    SELECT * FROM [core].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261111203221_AddReportSignatories'
)
BEGIN
    CREATE TABLE [lab].[ReportSignatories] (
        [Id] int NOT NULL IDENTITY,
        [OrganizationId] int NOT NULL,
        [NameEn] nvarchar(150) NOT NULL,
        [NameUr] nvarchar(150) NULL,
        [Qualification] nvarchar(200) NULL,
        [Designation] nvarchar(150) NULL,
        [RegistrationNo] nvarchar(50) NULL,
        [SignatureImage] varbinary(max) NULL,
        [SignatureContentType] varchar(50) NULL,
        [IsActive] bit NOT NULL,
        [CreatedAtUtc] datetime2(3) NOT NULL,
        [CreatedByUserId] int NULL,
        [UpdatedAtUtc] datetime2(3) NULL,
        [UpdatedByUserId] int NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_ReportSignatories] PRIMARY KEY ([Id]),
        CONSTRAINT [AK_ReportSignatories_OrganizationId_Id] UNIQUE ([OrganizationId], [Id]),
        CONSTRAINT [CK_ReportSignatories_Signature] CHECK ([SignatureImage] IS NULL OR (DATALENGTH([SignatureImage]) <= 204800 AND [SignatureContentType] IN ('image/png','image/jpeg'))),
        CONSTRAINT [FK_ReportSignatories_Organizations_OrganizationId] FOREIGN KEY ([OrganizationId]) REFERENCES [core].[Organizations] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [core].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261111203221_AddReportSignatories'
)
BEGIN
    CREATE INDEX [IX_ReportSignatories_OrganizationId_IsActive] ON [lab].[ReportSignatories] ([OrganizationId], [IsActive]);
END;

IF NOT EXISTS (
    SELECT * FROM [core].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261111203221_AddReportSignatories'
)
BEGIN
    INSERT INTO [core].[__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261111203221_AddReportSignatories', N'9.0.10');
END;

IF NOT EXISTS (
    SELECT * FROM [core].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261114065901_AddCommonTestParameters'
)
BEGIN
    SET NOCOUNT ON;
    IF NOT EXISTS (SELECT 1 FROM catalog.Units WHERE Code = 'U/mL') INSERT INTO catalog.Units (Code, Name, IsActive) VALUES ('U/mL', 'units per millilitre', 1);
    IF NOT EXISTS (SELECT 1 FROM catalog.Units WHERE Code = '10^6/mL') INSERT INTO catalog.Units (Code, Name, IsActive) VALUES ('10^6/mL', 'million per millilitre', 1);
    IF NOT EXISTS (SELECT 1 FROM catalog.Units WHERE Code = 'mIU/L') INSERT INTO catalog.Units (Code, Name, IsActive) VALUES ('mIU/L', 'milli-international units per litre', 1);
    DECLARE @u_uml int = (SELECT Id FROM catalog.Units WHERE Code = 'U/mL');
    DECLARE @u_106ml int = (SELECT Id FROM catalog.Units WHERE Code = '10^6/mL');
    DECLARE @u_miul int = (SELECT Id FROM catalog.Units WHERE Code = 'mIU/L');
    DECLARE @src nvarchar(300) = N'Typical adult reference values from general laboratory references. Verify against your analyser/kit insert and local practice before clinical use.';
    DECLARE @now datetime2(3) = SYSUTCDATETIME();

    -- Parameters (only where the test already exists in the organisation; safe to run twice)
    CREATE TABLE #p (TestCode varchar(20), Code varchar(20), NameEn nvarchar(200), ResultType int, UnitId int NULL,
                     DecimalPlaces int, GroupName nvarchar(100) NULL, SortOrder int);
    INSERT INTO #p (TestCode, Code, NameEn, ResultType, UnitId, DecimalPlaces, GroupName, SortOrder) VALUES
    ('ESR','ESR','ESR',1,30,0,NULL,10),
    ('TZ-0536','DDIMER','D-Dimer',1,4,2,NULL,10),
    ('TZ-0537','FDP','Fibrin degradation products',1,4,2,NULL,10),
    ('TZ-0428','FIB','Fibrinogen',1,2,2,NULL,10),
    ('TZ-0334','BT','Bleeding time',1,32,1,NULL,10),
    ('TZ-0386','CT','Clotting time',1,32,1,NULL,10),
    ('TZ-0087','F9','Factor IX activity',1,20,0,NULL,10),
    ('TZ-0089','F5','Factor V activity',1,20,0,NULL,10),
    ('TZ-0090','F7','Factor VII activity',1,20,0,NULL,10),
    ('TZ-0091','F8','Factor VIII activity',1,20,0,NULL,10),
    ('TZ-0092','F10','Factor X activity',1,20,0,NULL,10),
    ('TZ-0093','F11','Factor XI activity',1,20,0,NULL,10),
    ('TZ-0094','F13','Factor XIII activity',1,20,0,NULL,10),
    ('TZ-0717','AT3','Antithrombin III',1,20,0,NULL,10),
    ('TZ-0117','PROTC','Protein C',1,20,0,NULL,10),
    ('TZ-0118','PROTS','Protein S',1,20,0,NULL,10),
    ('TZ-0126','VWF','von Willebrand factor antigen',1,20,0,NULL,10),
    ('TZ-0497','LA','Lupus anticoagulant',2,NULL,0,NULL,10),
    ('TZ-0088','FVL','Factor V Leiden / APC resistance',2,NULL,0,NULL,10),
    ('TZ-0319','PT','Prothrombin time',1,31,1,NULL,10),
    ('TZ-0319','APTT','APTT',1,31,1,NULL,10),
    ('TZ-0319','INR','INR',1,33,2,NULL,10),
    ('TZ-0387','PT','Prothrombin time',1,31,1,NULL,10),
    ('TZ-0387','APTT','APTT',1,31,1,NULL,10),
    ('TZ-0387','INR','INR',1,33,2,NULL,10),
    ('TZ-0387','FIB','Fibrinogen',1,2,2,NULL,10),
    ('TZ-0424','PT','Prothrombin time',1,31,1,NULL,10),
    ('TZ-0424','APTT','APTT',1,31,1,NULL,10),
    ('TZ-0424','INR','INR',1,33,2,NULL,10),
    ('TZ-0424','FIB','Fibrinogen',1,2,2,NULL,10),
    ('TZ-0424','DDIMER','D-Dimer',1,4,2,NULL,10),
    ('TZ-0338','HB','Haemoglobin',1,1,1,'Complete blood count',10),
    ('TZ-0338','RBC','Red cell count',1,24,2,'Complete blood count',20),
    ('TZ-0338','WBC','Total leucocyte count',1,23,1,'Complete blood count',30),
    ('TZ-0338','PLT','Platelet count',1,23,0,'Complete blood count',40),
    ('TZ-0338','HCT','Haematocrit',1,20,1,'Complete blood count',50),
    ('TZ-0338','MCV','MCV',1,21,0,'Complete blood count',60),
    ('TZ-0338','MCH','MCH',1,22,1,'Complete blood count',70),
    ('TZ-0338','MCHC','MCHC',1,1,1,'Complete blood count',80),
    ('TZ-0338','RDW','RDW',1,20,1,'Complete blood count',90),
    ('TZ-0338','NEUT','Neutrophils',1,20,0,'Complete blood count',100),
    ('TZ-0338','LYMPH','Lymphocytes',1,20,0,'Complete blood count',110),
    ('TZ-0338','MONO','Monocytes',1,20,0,'Complete blood count',120),
    ('TZ-0338','EOS','Eosinophils',1,20,0,'Complete blood count',130),
    ('TZ-0338','BASO','Basophils',1,20,0,'Complete blood count',140),
    ('TZ-0417','NEUT','Neutrophils',1,20,0,'Differential count',10),
    ('TZ-0417','LYMPH','Lymphocytes',1,20,0,'Differential count',20),
    ('TZ-0417','MONO','Monocytes',1,20,0,'Differential count',30),
    ('TZ-0417','EOS','Eosinophils',1,20,0,'Differential count',40),
    ('TZ-0417','BASO','Basophils',1,20,0,'Differential count',50),
    ('TZ-0566','RETIC','Reticulocyte count',1,20,2,NULL,10),
    ('TZ-0526','FILM','Peripheral blood film',3,NULL,0,NULL,10),
    ('TZ-0562','RBCM','RBC morphology',3,NULL,0,NULL,10),
    ('TZ-0654','WBCM','WBC morphology',3,NULL,0,NULL,10),
    ('TZ-0579','SICKLE','Sickling test',2,NULL,0,NULL,10),
    ('TZ-0442','G6PD','G6PD screen',2,NULL,0,NULL,10),
    ('TZ-0463','HBEP','Haemoglobin pattern',3,NULL,0,NULL,10),
    ('TZ-0079','KARYO','Karyotype',3,NULL,0,NULL,10),
    ('TZ-0574','IRON','Serum iron',1,5,0,NULL,10),
    ('TZ-0853','TIBC','Total iron binding capacity',1,5,0,NULL,10),
    ('TZ-0860','TSAT','Transferrin saturation',1,20,0,NULL,10),
    ('TZ-0498','MG','Magnesium',1,3,2,NULL,10),
    ('TZ-0393','CCAL','Corrected calcium',1,3,2,NULL,10),
    ('TZ-0479','ICAL','Ionized calcium',1,10,2,NULL,10),
    ('TZ-0535','PHOS','Phosphate',1,3,2,NULL,10),
    ('TZ-0380','CL','Chloride',1,10,0,NULL,10),
    ('TZ-0323','HCO3','Bicarbonate',1,10,0,NULL,10),
    ('TZ-0335','NH3','Blood ammonia',1,11,0,NULL,10),
    ('TZ-0791','LACT','Lactate',1,10,2,NULL,10),
    ('TZ-0114','OSM','Osmolality',1,35,0,NULL,10),
    ('TZ-0494','LIP','Lipase',1,15,0,NULL,10),
    ('TZ-0304','AMY','Amylase',1,15,0,NULL,10),
    ('TZ-0394','CPK','Creatine kinase',1,15,0,NULL,10),
    ('TZ-0491','LDH','LDH',1,15,0,NULL,10),
    ('TZ-0630','UREA','Urea',1,3,0,NULL,10),
    ('TZ-0630','BUN','BUN',1,3,0,NULL,10),
    ('TZ-0400','CRCL','Creatinine clearance',1,34,0,NULL,10),
    ('TZ-0576','NONHDL','Non-HDL cholesterol',1,3,0,NULL,10),
    ('TZ-0652','VLDL','VLDL cholesterol',1,3,0,NULL,10),
    ('TZ-0287','AGRATIO','A/G ratio',1,33,2,NULL,10),
    ('TZ-0618','TP','Total protein',1,1,1,NULL,10),
    ('TZ-0485','KET','Ketone',2,NULL,0,NULL,10),
    ('TZ-0324','BILEA','Bile acids',1,11,1,NULL,10),
    ('TZ-0325','BILES','Bile salts',1,11,1,NULL,10),
    ('TZ-0813','PEP','Protein electrophoresis pattern',3,NULL,0,NULL,10),
    ('TZ-0383','CHOL','Cholesterol',1,3,0,NULL,10),
    ('TZ-0332','TBIL','Bilirubin, total',1,3,2,NULL,10),
    ('TZ-0443','GGT','Gamma GT',1,15,0,NULL,10),
    ('TZ-0398','CREAT24','Creatinine, 24 h urine',1,39,2,NULL,10),
    ('TZ-0633','UA24','Uric acid, 24 h urine',1,40,0,NULL,10),
    ('TZ-0627','UREA24','Urea, 24 h urine',1,39,1,NULL,10),
    ('TZ-0361','CA24','Calcium, 24 h urine',1,40,0,NULL,10),
    ('TZ-0385','CIT24','Citrate, 24 h urine',1,40,0,NULL,10),
    ('TZ-0534','PHOS24','Phosphate, 24 h urine',1,39,2,NULL,10),
    ('TZ-0546','KUR','Potassium, spot urine',1,10,0,NULL,10),
    ('TZ-0584','NAUR','Sodium, spot urine',1,10,0,NULL,10),
    ('TZ-0586','PHOSSP','Phosphate, spot urine',1,3,1,NULL,10),
    ('TZ-0645','UPH','Urine pH',1,NULL,1,NULL,10),
    ('TZ-0648','UGLU','Urine glucose',2,NULL,0,NULL,10),
    ('TZ-0603','UGLU','Urine glucose (fasting)',2,NULL,0,NULL,10),
    ('TZ-0320','NA','Sodium',1,10,0,'Basic metabolic panel',10),
    ('TZ-0320','K','Potassium',1,10,1,'Basic metabolic panel',20),
    ('TZ-0320','CL','Chloride',1,10,0,'Basic metabolic panel',30),
    ('TZ-0320','HCO3','Bicarbonate',1,10,0,'Basic metabolic panel',40),
    ('TZ-0320','UREA','Urea',1,3,0,'Basic metabolic panel',50),
    ('TZ-0320','CREAT','Creatinine',1,3,2,'Basic metabolic panel',60),
    ('TZ-0320','GLU','Glucose',1,3,0,'Basic metabolic panel',70),
    ('TZ-0740','CORT','Cortisol (morning)',1,5,2,NULL,10),
    ('TZ-0837','TESTO','Testosterone',1,8,0,NULL,10),
    ('TZ-0762','FTESTO','Free testosterone',1,9,2,NULL,10),
    ('TZ-0836','PRL','Prolactin',1,7,2,NULL,10),
    ('TZ-0833','FSH','FSH',1,17,2,NULL,10),
    ('TZ-0834','LH','LH',1,17,2,NULL,10),
    ('TZ-0756','E2','Estradiol',1,9,2,NULL,10),
    ('TZ-0812','PROG','Progesterone',1,7,2,NULL,10),
    ('TZ-0752','DHEAS','DHEA sulphate',1,5,1,NULL,10),
    ('TZ-0709','AMH','Anti-Mullerian hormone',1,7,2,NULL,10),
    ('TZ-0785','INS','Insulin (fasting)',1,18,2,NULL,10),
    ('TZ-0784','INS','Insulin (random)',1,18,2,NULL,10),
    ('TZ-0735','CPEP','C-peptide',1,7,2,NULL,10),
    ('TZ-0831','FOL','Folate',1,7,2,NULL,10),
    ('TZ-0814','PSA','PSA, total',1,7,2,NULL,10),
    ('TZ-0824','BHCG','Beta HCG',1,17,1,NULL,10),
    ('TZ-0822','AFP','Alpha fetoprotein',1,7,2,NULL,10),
    ('TZ-0738','CEA','CEA',1,7,2,NULL,10),
    ('TZ-0826','CA125','CA-125',1,@u_uml,1,NULL,10),
    ('TZ-0825','CA153','CA 15-3',1,@u_uml,1,NULL,10),
    ('TZ-0827','CA199','CA 19-9',1,@u_uml,1,NULL,10),
    ('TZ-0845','TG','Thyroglobulin',1,7,2,NULL,10),
    ('TZ-0847','ATPO','Anti-thyroid peroxidase',1,19,1,NULL,10),
    ('TZ-0718','ATG','Anti-thyroglobulin',1,19,1,NULL,10),
    ('TZ-0863','TRAB','TSH receptor antibodies',1,19,2,NULL,10),
    ('TZ-0787','PTH','Parathyroid hormone',1,9,1,NULL,10),
    ('TZ-0080','CALC','Calcitonin',1,9,1,NULL,10),
    ('TZ-0767','GH','Growth hormone',1,7,2,NULL,10),
    ('TZ-0664','ACTH','ACTH',1,9,1,NULL,10),
    ('TZ-0670','ALD','Aldosterone',1,8,2,NULL,10),
    ('TZ-0776','HCYS','Homocysteine',1,11,1,NULL,10),
    ('TZ-0311','ASOT','ASO titre',1,19,0,NULL,10),
    ('TZ-0561','RF','Rheumatoid factor',1,19,0,NULL,10),
    ('TZ-0742','CRP','C-reactive protein',1,4,2,NULL,10),
    ('TZ-0721','ACCP','Anti-CCP',1,@u_uml,1,NULL,10),
    ('TZ-0685','ADSDNA','Anti-dsDNA IgG',1,19,1,NULL,10),
    ('TZ-0682','ACLIGG','Anti-cardiolipin IgG',1,@u_uml,1,NULL,10),
    ('TZ-0683','ACLIM','Anti-cardiolipin IgM',1,@u_uml,1,NULL,10),
    ('TZ-0309','ANTIHCV','Anti-HCV',2,NULL,0,NULL,10),
    ('TZ-0459','HBSAG','HBsAg',2,NULL,0,NULL,10),
    ('TZ-0701','ANTIHIV','Anti-HIV 1/2',2,NULL,0,NULL,10),
    ('TZ-0610','VDRL','VDRL',2,NULL,0,NULL,10),
    ('TZ-0859','TPHA','TPHA',2,NULL,0,NULL,10),
    ('TZ-0416','NS1','Dengue NS1 antigen',2,NULL,0,NULL,10),
    ('TZ-0415','DENIGM','Dengue IgM',2,NULL,0,NULL,10),
    ('TZ-0414','DENIGG','Dengue IgG',2,NULL,0,NULL,10),
    ('TZ-0308','HPYL','Anti-H. pylori',2,NULL,0,NULL,10),
    ('TZ-0457','HPAG','H. pylori antigen (stool)',2,NULL,0,NULL,10),
    ('TZ-0673','ANA','ANA (IFA)',2,NULL,0,NULL,10),
    ('TZ-0641','CULT','Culture report',3,NULL,0,NULL,10),
    ('TZ-0643','GRAM','Gram stain',3,NULL,0,NULL,10),
    ('TZ-0600','CULT','Culture report',3,NULL,0,NULL,10),
    ('TZ-0452','GRAM','Gram stain',3,NULL,0,NULL,10),
    ('TZ-0531','GRAM','Gram stain',3,NULL,0,NULL,10),
    ('TZ-0598','COLOUR','Colour',2,NULL,0,'Stool routine examination',10),
    ('TZ-0598','CONS','Consistency',2,NULL,0,'Stool routine examination',20),
    ('TZ-0598','OVA','Ova / cysts / parasites',3,NULL,0,'Stool routine examination',30),
    ('TZ-0598','OB','Occult blood',2,NULL,0,'Stool routine examination',40),
    ('TZ-0601','OB','Occult blood',2,NULL,0,NULL,10),
    ('TZ-0570','VOL','Volume',1,38,1,'Semen analysis',10),
    ('TZ-0570','COUNT','Sperm concentration',1,@u_106ml,1,'Semen analysis',20),
    ('TZ-0570','MOTILE','Motility (progressive)',1,20,0,'Semen analysis',30),
    ('TZ-0570','MORPH','Normal morphology',1,20,0,'Semen analysis',40),
    ('TZ-0570','SPH','pH',1,NULL,1,'Semen analysis',50);
    INSERT INTO catalog.TestParameters
        (OrganizationId, TestId, Code, NameEn, ResultType, UnitId, DecimalPlaces, GroupName, SortOrder, IsActive, CreatedAtUtc, CreatedByUserId)
    SELECT t.OrganizationId, t.Id, s.Code, s.NameEn, s.ResultType, s.UnitId, s.DecimalPlaces, s.GroupName, s.SortOrder, 1, @now, NULL
    FROM #p s
    JOIN catalog.Tests t ON t.Code = s.TestCode AND t.IsDeleted = 0
    WHERE NOT EXISTS (SELECT 1 FROM catalog.TestParameters p WHERE p.TestId = t.Id AND p.Code = s.Code);

    CREATE TABLE #r (TestCode varchar(20), Code varchar(20), Sex int, Low decimal(18,4) NULL, High decimal(18,4) NULL,
                     DisplayText nvarchar(200) NULL, CriticalLow decimal(18,4) NULL, CriticalHigh decimal(18,4) NULL);
    INSERT INTO #r (TestCode, Code, Sex, Low, High, DisplayText, CriticalLow, CriticalHigh) VALUES
    ('ESR','ESR',1,0,15,'< 15',NULL,NULL),
    ('ESR','ESR',2,0,20,'< 20',NULL,NULL),
    ('TZ-0536','DDIMER',0,0,0.5,'< 0.50',NULL,NULL),
    ('TZ-0537','FDP',0,0,5.0,'< 5.0',NULL,NULL),
    ('TZ-0428','FIB',0,2.0,4.0,NULL,NULL,NULL),
    ('TZ-0334','BT',0,2,7,NULL,NULL,NULL),
    ('TZ-0386','CT',0,4,10,NULL,NULL,NULL),
    ('TZ-0087','F9',0,50,150,NULL,NULL,NULL),
    ('TZ-0089','F5',0,50,150,NULL,NULL,NULL),
    ('TZ-0090','F7',0,50,150,NULL,NULL,NULL),
    ('TZ-0091','F8',0,50,150,NULL,NULL,NULL),
    ('TZ-0092','F10',0,50,150,NULL,NULL,NULL),
    ('TZ-0093','F11',0,50,150,NULL,NULL,NULL),
    ('TZ-0094','F13',0,50,150,NULL,NULL,NULL),
    ('TZ-0717','AT3',0,80,120,NULL,NULL,NULL),
    ('TZ-0117','PROTC',0,70,140,NULL,NULL,NULL),
    ('TZ-0118','PROTS',0,65,140,NULL,NULL,NULL),
    ('TZ-0126','VWF',0,50,160,NULL,NULL,NULL),
    ('TZ-0319','PT',0,11,14,NULL,NULL,NULL),
    ('TZ-0319','APTT',0,25,35,NULL,NULL,NULL),
    ('TZ-0319','INR',0,0.8,1.2,NULL,NULL,NULL),
    ('TZ-0387','PT',0,11,14,NULL,NULL,NULL),
    ('TZ-0387','APTT',0,25,35,NULL,NULL,NULL),
    ('TZ-0387','INR',0,0.8,1.2,NULL,NULL,NULL),
    ('TZ-0387','FIB',0,2.0,4.0,NULL,NULL,NULL),
    ('TZ-0424','PT',0,11,14,NULL,NULL,NULL),
    ('TZ-0424','APTT',0,25,35,NULL,NULL,NULL),
    ('TZ-0424','INR',0,0.8,1.2,NULL,NULL,NULL),
    ('TZ-0424','FIB',0,2.0,4.0,NULL,NULL,NULL),
    ('TZ-0424','DDIMER',0,0,0.5,'< 0.50',NULL,NULL),
    ('TZ-0338','HB',1,13,17,'13 - 17',NULL,NULL),
    ('TZ-0338','HB',2,12,15,'12 - 15',NULL,NULL),
    ('TZ-0338','RBC',1,4.5,5.9,'4.5 - 5.9',NULL,NULL),
    ('TZ-0338','RBC',2,4.1,5.1,'4.1 - 5.1',NULL,NULL),
    ('TZ-0338','WBC',0,4,11,NULL,NULL,NULL),
    ('TZ-0338','PLT',0,150,450,NULL,NULL,NULL),
    ('TZ-0338','HCT',1,40,52,'40 - 52',NULL,NULL),
    ('TZ-0338','HCT',2,36,48,'36 - 48',NULL,NULL),
    ('TZ-0338','MCV',0,80,100,NULL,NULL,NULL),
    ('TZ-0338','MCH',0,27,33,NULL,NULL,NULL),
    ('TZ-0338','MCHC',0,32,36,NULL,NULL,NULL),
    ('TZ-0338','RDW',0,11.5,14.5,NULL,NULL,NULL),
    ('TZ-0338','NEUT',0,40,75,NULL,NULL,NULL),
    ('TZ-0338','LYMPH',0,20,45,NULL,NULL,NULL),
    ('TZ-0338','MONO',0,2,10,NULL,NULL,NULL),
    ('TZ-0338','EOS',0,1,6,NULL,NULL,NULL),
    ('TZ-0338','BASO',0,0,2,NULL,NULL,NULL),
    ('TZ-0417','NEUT',0,40,75,NULL,NULL,NULL),
    ('TZ-0417','LYMPH',0,20,45,NULL,NULL,NULL),
    ('TZ-0417','MONO',0,2,10,NULL,NULL,NULL),
    ('TZ-0417','EOS',0,1,6,NULL,NULL,NULL),
    ('TZ-0417','BASO',0,0,2,NULL,NULL,NULL),
    ('TZ-0566','RETIC',0,0.5,2.5,NULL,NULL,NULL),
    ('TZ-0574','IRON',1,65,175,'65 - 175',NULL,NULL),
    ('TZ-0574','IRON',2,50,170,'50 - 170',NULL,NULL),
    ('TZ-0853','TIBC',0,250,450,NULL,NULL,NULL),
    ('TZ-0860','TSAT',0,20,50,NULL,NULL,NULL),
    ('TZ-0498','MG',0,1.7,2.2,NULL,NULL,NULL),
    ('TZ-0393','CCAL',0,8.5,10.2,NULL,NULL,NULL),
    ('TZ-0479','ICAL',0,1.1,1.3,NULL,NULL,NULL),
    ('TZ-0535','PHOS',0,2.5,4.5,NULL,NULL,NULL),
    ('TZ-0380','CL',0,98,107,NULL,NULL,NULL),
    ('TZ-0323','HCO3',0,22,29,NULL,NULL,NULL),
    ('TZ-0335','NH3',0,15,45,NULL,NULL,NULL),
    ('TZ-0791','LACT',0,0.5,2.2,NULL,NULL,NULL),
    ('TZ-0114','OSM',0,275,295,NULL,NULL,NULL),
    ('TZ-0494','LIP',0,0,60,NULL,NULL,NULL),
    ('TZ-0304','AMY',0,30,110,NULL,NULL,NULL),
    ('TZ-0394','CPK',1,30,200,'30 - 200',NULL,NULL),
    ('TZ-0394','CPK',2,25,170,'25 - 170',NULL,NULL),
    ('TZ-0491','LDH',0,140,280,NULL,NULL,NULL),
    ('TZ-0630','UREA',0,15,40,NULL,NULL,NULL),
    ('TZ-0630','BUN',0,7,20,NULL,NULL,NULL),
    ('TZ-0400','CRCL',0,90,140,NULL,NULL,NULL),
    ('TZ-0576','NONHDL',0,0,130,'< 130',NULL,NULL),
    ('TZ-0652','VLDL',0,5,40,NULL,NULL,NULL),
    ('TZ-0287','AGRATIO',0,1.0,2.1,NULL,NULL,NULL),
    ('TZ-0618','TP',0,6.4,8.3,NULL,NULL,NULL),
    ('TZ-0324','BILEA',0,0,10,'< 10',NULL,NULL),
    ('TZ-0325','BILES',0,0,10,'< 10',NULL,NULL),
    ('TZ-0383','CHOL',0,0,200,'< 200',NULL,NULL),
    ('TZ-0332','TBIL',0,0.3,1.2,NULL,NULL,NULL),
    ('TZ-0443','GGT',1,0,55,'0 - 55',NULL,NULL),
    ('TZ-0443','GGT',2,0,38,'0 - 38',NULL,NULL),
    ('TZ-0398','CREAT24',0,1.0,2.0,NULL,NULL,NULL),
    ('TZ-0633','UA24',0,250,750,NULL,NULL,NULL),
    ('TZ-0627','UREA24',0,12,20,NULL,NULL,NULL),
    ('TZ-0361','CA24',0,100,300,NULL,NULL,NULL),
    ('TZ-0385','CIT24',0,320,1240,NULL,NULL,NULL),
    ('TZ-0534','PHOS24',0,0.4,1.3,NULL,NULL,NULL),
    ('TZ-0546','KUR',0,25,125,NULL,NULL,NULL),
    ('TZ-0584','NAUR',0,40,220,NULL,NULL,NULL),
    ('TZ-0586','PHOSSP',0,NULL,NULL,'Report with urine creatinine',NULL,NULL),
    ('TZ-0645','UPH',0,5.0,8.0,NULL,NULL,NULL),
    ('TZ-0320','NA',0,135,145,NULL,NULL,NULL),
    ('TZ-0320','K',0,3.5,5.1,NULL,NULL,NULL),
    ('TZ-0320','CL',0,98,107,NULL,NULL,NULL),
    ('TZ-0320','HCO3',0,22,29,NULL,NULL,NULL),
    ('TZ-0320','UREA',0,15,40,NULL,NULL,NULL),
    ('TZ-0320','CREAT',0,0.6,1.3,NULL,NULL,NULL),
    ('TZ-0320','GLU',0,70,100,NULL,NULL,NULL),
    ('TZ-0740','CORT',0,6.2,19.4,NULL,NULL,NULL),
    ('TZ-0837','TESTO',1,300,1000,'300 - 1000',NULL,NULL),
    ('TZ-0837','TESTO',2,15,70,'15 - 70',NULL,NULL),
    ('TZ-0762','FTESTO',1,50,210,'50 - 210',NULL,NULL),
    ('TZ-0762','FTESTO',2,0.5,6.5,'0.5 - 6.5',NULL,NULL),
    ('TZ-0836','PRL',1,4,15,'4 - 15',NULL,NULL),
    ('TZ-0836','PRL',2,5,25,'5 - 25',NULL,NULL),
    ('TZ-0833','FSH',1,1.5,12.4,'1.5 - 12.4',NULL,NULL),
    ('TZ-0833','FSH',2,3.5,12.5,'3.5 - 12.5 (follicular)',NULL,NULL),
    ('TZ-0834','LH',1,1.7,8.6,'1.7 - 8.6',NULL,NULL),
    ('TZ-0834','LH',2,2.4,12.6,'2.4 - 12.6 (follicular)',NULL,NULL),
    ('TZ-0756','E2',1,10,40,'10 - 40',NULL,NULL),
    ('TZ-0756','E2',2,30,400,'30 - 400 (follicular)',NULL,NULL),
    ('TZ-0812','PROG',1,0,1,'< 1',NULL,NULL),
    ('TZ-0812','PROG',2,0.2,25,'0.2 - 25 (luteal)',NULL,NULL),
    ('TZ-0752','DHEAS',1,80,560,'80 - 560',NULL,NULL),
    ('TZ-0752','DHEAS',2,35,430,'35 - 430',NULL,NULL),
    ('TZ-0709','AMH',0,1.0,4.0,'1.0 - 4.0 (reproductive age)',NULL,NULL),
    ('TZ-0785','INS',0,2.6,24.9,NULL,NULL,NULL),
    ('TZ-0784','INS',0,2.6,24.9,NULL,NULL,NULL),
    ('TZ-0735','CPEP',0,0.8,3.9,NULL,NULL,NULL),
    ('TZ-0831','FOL',0,3.0,17.0,NULL,NULL,NULL),
    ('TZ-0814','PSA',0,0,4.0,'< 4.0',NULL,NULL),
    ('TZ-0824','BHCG',0,0,5,'< 5',NULL,NULL),
    ('TZ-0822','AFP',0,0,10,'< 10',NULL,NULL),
    ('TZ-0738','CEA',0,0,3.0,'< 3.0',NULL,NULL),
    ('TZ-0826','CA125',0,0,35,'< 35',NULL,NULL),
    ('TZ-0825','CA153',0,0,31.3,'< 31.3',NULL,NULL),
    ('TZ-0827','CA199',0,0,37,'< 37',NULL,NULL),
    ('TZ-0845','TG',0,1.4,78,NULL,NULL,NULL),
    ('TZ-0847','ATPO',0,0,35,'< 35',NULL,NULL),
    ('TZ-0718','ATG',0,0,40,'< 40',NULL,NULL),
    ('TZ-0863','TRAB',0,0,1.75,'< 1.75',NULL,NULL),
    ('TZ-0787','PTH',0,15,65,NULL,NULL,NULL),
    ('TZ-0080','CALC',1,0,8.4,'< 8.4',NULL,NULL),
    ('TZ-0080','CALC',2,0,5.0,'< 5.0',NULL,NULL),
    ('TZ-0767','GH',0,0,3.0,'< 3.0 (random)',NULL,NULL),
    ('TZ-0664','ACTH',0,7.2,63.3,NULL,NULL,NULL),
    ('TZ-0670','ALD',0,4,31,'4 - 31 (upright)',NULL,NULL),
    ('TZ-0776','HCYS',0,5,15,NULL,NULL,NULL),
    ('TZ-0311','ASOT',0,0,200,'< 200',NULL,NULL),
    ('TZ-0561','RF',0,0,14,'< 14',NULL,NULL),
    ('TZ-0742','CRP',0,0,5,'< 5',NULL,NULL),
    ('TZ-0721','ACCP',0,0,20,'< 20',NULL,NULL),
    ('TZ-0685','ADSDNA',0,0,30,'< 30',NULL,NULL),
    ('TZ-0682','ACLIGG',0,0,20,'< 20',NULL,NULL),
    ('TZ-0683','ACLIM',0,0,20,'< 20',NULL,NULL),
    ('TZ-0570','VOL',0,1.4,5.0,NULL,NULL,NULL),
    ('TZ-0570','COUNT',0,15,NULL,'>= 15',NULL,NULL),
    ('TZ-0570','MOTILE',0,40,NULL,'>= 40',NULL,NULL),
    ('TZ-0570','MORPH',0,4,NULL,'>= 4',NULL,NULL),
    ('TZ-0570','SPH',0,7.2,8.0,NULL,NULL,NULL);
    INSERT INTO catalog.ReferenceRanges
        (OrganizationId, ParameterId, Sex, AgeFromDays, AgeToDays, Low, High, CriticalLow, CriticalHigh,
         DisplayText, Source, Status, ApprovedByUserId, ApprovedAtUtc, CreatedAtUtc, CreatedByUserId)
    SELECT p.OrganizationId, p.Id, s.Sex, 6570, NULL, s.Low, s.High, s.CriticalLow, s.CriticalHigh,
           s.DisplayText, @src, 1, ap.Id, @now, @now, NULL
    FROM #r s
    JOIN catalog.Tests t ON t.Code = s.TestCode AND t.IsDeleted = 0
    JOIN catalog.TestParameters p ON p.TestId = t.Id AND p.Code = s.Code
    CROSS APPLY (SELECT MIN(u.Id) AS Id FROM security.Users u WHERE u.OrganizationId = p.OrganizationId) ap
    WHERE ap.Id IS NOT NULL
      AND NOT EXISTS (SELECT 1 FROM catalog.ReferenceRanges r WHERE r.ParameterId = p.Id AND r.Sex = s.Sex);

    CREATE TABLE #o (TestCode varchar(20), Code varchar(20), ValueEn nvarchar(100), IsAbnormal bit, SortOrder int);
    INSERT INTO #o (TestCode, Code, ValueEn, IsAbnormal, SortOrder) VALUES
    ('TZ-0497','LA','Negative',0,0),
    ('TZ-0497','LA','Positive',1,10),
    ('TZ-0088','FVL','Not detected',0,0),
    ('TZ-0088','FVL','Detected',1,10),
    ('TZ-0579','SICKLE','Negative',0,0),
    ('TZ-0579','SICKLE','Positive',1,10),
    ('TZ-0442','G6PD','Normal',0,0),
    ('TZ-0442','G6PD','Deficient',1,10),
    ('TZ-0485','KET','Negative',0,0),
    ('TZ-0485','KET','Positive',1,10),
    ('TZ-0648','UGLU','Negative',0,0),
    ('TZ-0648','UGLU','Trace',1,10),
    ('TZ-0648','UGLU','Positive',1,20),
    ('TZ-0603','UGLU','Negative',0,0),
    ('TZ-0603','UGLU','Trace',1,10),
    ('TZ-0603','UGLU','Positive',1,20),
    ('TZ-0309','ANTIHCV','Non-reactive',0,0),
    ('TZ-0309','ANTIHCV','Reactive',1,10),
    ('TZ-0459','HBSAG','Non-reactive',0,0),
    ('TZ-0459','HBSAG','Reactive',1,10),
    ('TZ-0701','ANTIHIV','Non-reactive',0,0),
    ('TZ-0701','ANTIHIV','Reactive',1,10),
    ('TZ-0610','VDRL','Non-reactive',0,0),
    ('TZ-0610','VDRL','Reactive',1,10),
    ('TZ-0859','TPHA','Non-reactive',0,0),
    ('TZ-0859','TPHA','Reactive',1,10),
    ('TZ-0416','NS1','Negative',0,0),
    ('TZ-0416','NS1','Positive',1,10),
    ('TZ-0415','DENIGM','Negative',0,0),
    ('TZ-0415','DENIGM','Positive',1,10),
    ('TZ-0414','DENIGG','Negative',0,0),
    ('TZ-0414','DENIGG','Positive',1,10),
    ('TZ-0308','HPYL','Negative',0,0),
    ('TZ-0308','HPYL','Positive',1,10),
    ('TZ-0457','HPAG','Negative',0,0),
    ('TZ-0457','HPAG','Positive',1,10),
    ('TZ-0673','ANA','Negative',0,0),
    ('TZ-0673','ANA','Positive',1,10),
    ('TZ-0598','COLOUR','Brown',0,0),
    ('TZ-0598','COLOUR','Yellow',0,10),
    ('TZ-0598','COLOUR','Green',0,20),
    ('TZ-0598','COLOUR','Black',1,30),
    ('TZ-0598','COLOUR','Blood-stained',1,40),
    ('TZ-0598','CONS','Formed',0,0),
    ('TZ-0598','CONS','Semi-formed',0,10),
    ('TZ-0598','CONS','Loose',1,20),
    ('TZ-0598','CONS','Watery',1,30),
    ('TZ-0598','OB','Negative',0,0),
    ('TZ-0598','OB','Positive',1,10),
    ('TZ-0601','OB','Negative',0,0),
    ('TZ-0601','OB','Positive',1,10);
    INSERT INTO catalog.ParameterOptions (OrganizationId, ParameterId, ValueEn, IsAbnormal, SortOrder)
    SELECT p.OrganizationId, p.Id, s.ValueEn, s.IsAbnormal, s.SortOrder
    FROM #o s
    JOIN catalog.Tests t ON t.Code = s.TestCode AND t.IsDeleted = 0
    JOIN catalog.TestParameters p ON p.TestId = t.Id AND p.Code = s.Code
    WHERE NOT EXISTS (SELECT 1 FROM catalog.ParameterOptions o WHERE o.ParameterId = p.Id AND o.ValueEn = s.ValueEn);

    DROP TABLE #p; DROP TABLE #r; DROP TABLE #o;
END;

IF NOT EXISTS (
    SELECT * FROM [core].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261114065901_AddCommonTestParameters'
)
BEGIN
    INSERT INTO [core].[__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261114065901_AddCommonTestParameters', N'9.0.10');
END;

COMMIT;
GO

