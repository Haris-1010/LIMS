using Lims.Domain.Catalog;

namespace Lims.Infrastructure.Catalog;

/// <summary>
/// The commonly ordered tests a new laboratory starts with (owner, 2026-11-12): tests, parameters, options and
/// reference ranges. Every range is created as a <b>draft</b>: nothing flags a result until someone with range
/// approval checks it against the laboratory's own analyser and kit insert and approves it. Prices start at 0.
/// Numeric ranges are typical adult values (from 18 years); children get no range until the laboratory adds one.
/// Codes are stable: a later version may append tests, never renumber or rename a code.
/// </summary>
public static class StarterCatalog
{
    /// <summary>Written to the organisation's business settings once the starter catalog has been added.</summary>
    public const string MarkerKey = "Catalog.StarterCatalog";
    public const string Version = "1";

    public const string RangeSource =
        "Starter catalog: typical adult values from general laboratory references. Check against your analyser and kit insert, edit if needed, then approve.";

    /// <summary>18 years in days: numeric starter ranges apply from this age.</summary>
    public const int AdultFromDays = 18 * 365;

    public sealed record Category(string Code, string En, string Ur);

    public sealed record Test(string Code, string En, string Ur, string Short, string Category, string Department, string SampleType,
        int TurnaroundHours, Parameter[] Parameters);

    public sealed record Parameter(string Code, string En, ResultType Type, string? Unit, int Decimals, string? Group,
        Option[] Options, Range[] Ranges);

    public sealed record Option(string En, string Ur, bool Abnormal = false);

    /// <summary>A starter range. <paramref name="AllAges"/> = applies from birth (option and text values); otherwise from 18 years.</summary>
    public sealed record Range(RangeSex Sex, decimal? Low, decimal? High, decimal? CriticalLow = null, decimal? CriticalHigh = null,
        string? Text = null, bool AllAges = false);

    public static readonly Category[] Categories =
    [
        new("HAEMATOLOGY", "Haematology", "ہیماٹولوجی"),
        new("COAGULATION", "Coagulation", "خون جمنے کے ٹیسٹ"),
        new("BIOCHEMISTRY", "Biochemistry", "بائیو کیمسٹری"),
        new("HORMONES", "Hormones & Vitamins", "ہارمونز اور وٹامنز"),
        new("SEROLOGY", "Serology", "سیرولوجی"),
        new("URINE-STOOL", "Urine & Stool", "پیشاب اور پاخانہ"),
    ];

    // ---- small helpers that keep the table readable ----
    private static Parameter Num(string code, string en, string? unit, int decimals, params Range[] ranges) =>
        new(code, en, ResultType.Numeric, unit, decimals, null, [], ranges);

    private static Parameter Grouped(string group, Parameter p) => p with { Group = group };

    private static Range Any(decimal? low, decimal? high, decimal? critLow = null, decimal? critHigh = null, string? text = null) =>
        new(RangeSex.Any, low, high, critLow, critHigh, text);

    private static Range Male(decimal? low, decimal? high, decimal? critLow = null, decimal? critHigh = null, string? text = null) =>
        new(RangeSex.Male, low, high, critLow, critHigh, text);

    private static Range Female(decimal? low, decimal? high, decimal? critLow = null, decimal? critHigh = null, string? text = null) =>
        new(RangeSex.Female, low, high, critLow, critHigh, text);

    private static Range Normal(string text) => new(RangeSex.Any, null, null, Text: text, AllAges: true);

    private static readonly Option[] ReactiveOptions = [new("Non-reactive", "غیر ری ایکٹو"), new("Reactive", "ری ایکٹو", true)];
    private static readonly Option[] PositiveOptions = [new("Negative", "منفی"), new("Positive", "مثبت", true)];
    private static readonly Option[] NilToPlus3 =
    [
        new("Nil", "نہیں"), new("Trace", "معمولی", true), new("+", "+", true), new("++", "++", true), new("+++", "+++", true),
    ];

    private static Parameter Reactive(string code, string en) =>
        new(code, en, ResultType.Option, null, 0, null, ReactiveOptions, [Normal("Non-reactive")]);

    private static Parameter Positive(string code, string en) =>
        new(code, en, ResultType.Option, null, 0, null, PositiveOptions, [Normal("Negative")]);

    private static Parameter Text(string code, string en, string? unit, string? group, string normal) =>
        new(code, en, ResultType.Text, unit, 0, group, [], [Normal(normal)]);

    private static Parameter Choice(string code, string en, string? group, Option[] options, string normal) =>
        new(code, en, ResultType.Option, null, 0, group, options, [Normal(normal)]);

    public static readonly Test[] Tests =
    [
        // ================= Haematology =================
        new("CBC", "Complete Blood Count", "مکمل بلڈ کاؤنٹ", "CBC", "HAEMATOLOGY", "HAEM", "WB-EDTA", 4,
        [
            Num("HB", "Haemoglobin", "g/dL", 1, Male(13.0m, 17.0m, 7.0m, 20.0m), Female(12.0m, 15.0m, 7.0m, 20.0m)),
            Num("RBC", "Red blood cell count", "×10⁶/µL", 2, Male(4.5m, 5.5m), Female(3.8m, 4.8m)),
            Num("HCT", "Haematocrit (PCV)", "%", 1, Male(40m, 50m), Female(36m, 46m)),
            Num("MCV", "MCV", "fL", 1, Any(83m, 101m)),
            Num("MCH", "MCH", "pg", 1, Any(27m, 32m)),
            Num("MCHC", "MCHC", "g/dL", 1, Any(31.5m, 34.5m)),
            Num("RDW", "RDW-CV", "%", 1, Any(11.6m, 14.0m)),
            Num("WBC", "Total leucocyte count (TLC)", "×10³/µL", 1, Any(4.0m, 10.0m, 2.0m, 30.0m)),
            Grouped("Differential count", Num("NEUT", "Neutrophils", "%", 0, Any(40m, 80m))),
            Grouped("Differential count", Num("LYMPH", "Lymphocytes", "%", 0, Any(20m, 40m))),
            Grouped("Differential count", Num("MONO", "Monocytes", "%", 0, Any(2m, 10m))),
            Grouped("Differential count", Num("EOS", "Eosinophils", "%", 0, Any(1m, 6m))),
            Grouped("Differential count", Num("BASO", "Basophils", "%", 0, Any(0m, 2m))),
            Num("PLT", "Platelet count", "×10³/µL", 0, Any(150m, 410m, 20m, 1000m)),
        ]),
        new("ESR", "Erythrocyte Sedimentation Rate", "ای ایس آر", "ESR", "HAEMATOLOGY", "HAEM", "WB-EDTA", 4,
        [
            Num("ESR", "ESR (Westergren, 1st hour)", "mm/hr", 0, Male(0m, 15m), Female(0m, 20m)),
        ]),
        new("BG", "Blood Group & Rh Factor", "بلڈ گروپ", "Blood group", "HAEMATOLOGY", "HAEM", "WB-EDTA", 2,
        [
            new("ABO", "ABO group", ResultType.Option, null, 0, null,
                [new("A", "A"), new("B", "B"), new("AB", "AB"), new("O", "O")], []),
            new("RH", "Rh (D)", ResultType.Option, null, 0, null,
                [new("Positive", "پازیٹو"), new("Negative", "نیگیٹو")], []),
        ]),
        new("MP", "Malaria Parasite (Smear)", "ملیریا ٹیسٹ", "MP", "HAEMATOLOGY", "HAEM", "WB-EDTA", 4,
        [
            Choice("MP", "Malaria parasite", null,
                [new("Not seen", "نظر نہیں آیا"), new("P. vivax seen", "پی ویویکس موجود", true), new("P. falciparum seen", "پی فالسیپیرم موجود", true),
                 new("Mixed infection seen", "مخلوط انفیکشن موجود", true)], "Not seen"),
        ]),

        // ================= Coagulation =================
        new("PT", "Prothrombin Time (PT / INR)", "پروتھرومبن ٹائم", "PT/INR", "COAGULATION", "HAEM", "PL-CIT", 4,
        [
            Num("PT", "Prothrombin time (patient)", "sec", 1, Any(11.0m, 13.5m)),
            Num("PTC", "Prothrombin time (control)", "sec", 1),
            Num("INR", "INR", "ratio", 2, Any(0.8m, 1.2m, null, 5.0m)),
        ]),
        new("APTT", "Activated Partial Thromboplastin Time", "اے پی ٹی ٹی", "APTT", "COAGULATION", "HAEM", "PL-CIT", 4,
        [
            Num("APTT", "APTT (patient)", "sec", 1, Any(25.0m, 35.0m)),
            Num("APTTC", "APTT (control)", "sec", 1),
        ]),

        // ================= Biochemistry =================
        new("FBS", "Blood Glucose (Fasting)", "شوگر (نہار منہ)", "FBS", "BIOCHEMISTRY", "CHEM", "PL-FLU", 2,
        [
            Num("GLUF", "Glucose, fasting", "mg/dL", 0, Any(70m, 99m, 40m, 500m)),
        ]),
        new("RBS", "Blood Glucose (Random)", "شوگر (رینڈم)", "RBS", "BIOCHEMISTRY", "CHEM", "PL-FLU", 2,
        [
            Num("GLUR", "Glucose, random", "mg/dL", 0, Any(70m, 140m, 40m, 500m)),
        ]),
        new("HBA1C", "Glycated Haemoglobin (HbA1c)", "ایچ بی اے ون سی", "HbA1c", "BIOCHEMISTRY", "CHEM", "WB-EDTA", 24,
        [
            Num("HBA1C", "HbA1c", "%", 1, Any(null, 5.6m, text: "< 5.7 (prediabetes 5.7 – 6.4, diabetes ≥ 6.5)")),
        ]),
        new("LFT", "Liver Function Tests", "جگر کے ٹیسٹ (ایل ایف ٹی)", "LFT", "BIOCHEMISTRY", "CHEM", "SERUM", 6,
        [
            Num("TBIL", "Bilirubin, total", "mg/dL", 2, Any(0.3m, 1.2m)),
            Num("DBIL", "Bilirubin, direct", "mg/dL", 2, Any(0.0m, 0.3m)),
            Num("ALT", "ALT (SGPT)", "U/L", 0, Male(null, 41m, text: "≤ 41"), Female(null, 33m, text: "≤ 33")),
            Num("AST", "AST (SGOT)", "U/L", 0, Male(null, 40m, text: "≤ 40"), Female(null, 32m, text: "≤ 32")),
            Num("ALP", "Alkaline phosphatase", "U/L", 0, Male(40m, 129m), Female(35m, 104m)),
            Num("TP", "Total protein", "g/dL", 1, Any(6.4m, 8.3m)),
            Num("ALB", "Albumin", "g/dL", 1, Any(3.5m, 5.2m)),
            Num("GLOB", "Globulin", "g/dL", 1, Any(2.0m, 3.5m)),
        ]),
        new("RFT", "Renal Function Tests", "گردوں کے ٹیسٹ (آر ایف ٹی)", "RFT", "BIOCHEMISTRY", "CHEM", "SERUM", 6,
        [
            Num("UREA", "Urea", "mg/dL", 0, Any(15m, 45m)),
            Num("CREAT", "Creatinine", "mg/dL", 2, Male(0.7m, 1.3m), Female(0.6m, 1.1m)),
        ]),
        new("ELEC", "Serum Electrolytes", "الیکٹرولائٹس", "Electrolytes", "BIOCHEMISTRY", "CHEM", "SERUM", 6,
        [
            Num("NA", "Sodium", "mmol/L", 0, Any(135m, 145m, 120m, 160m)),
            Num("K", "Potassium", "mmol/L", 1, Any(3.5m, 5.1m, 2.5m, 6.5m)),
            Num("CL", "Chloride", "mmol/L", 0, Any(98m, 107m)),
            Num("HCO3", "Bicarbonate", "mmol/L", 0, Any(22m, 29m)),
        ]),
        new("LIPID", "Lipid Profile", "لپڈ پروفائل", "Lipid profile", "BIOCHEMISTRY", "CHEM", "SERUM", 6,
        [
            Num("CHOL", "Cholesterol, total", "mg/dL", 0, Any(null, 200m, text: "< 200 (desirable)")),
            Num("TG", "Triglycerides", "mg/dL", 0, Any(null, 150m, text: "< 150")),
            Num("HDL", "HDL cholesterol", "mg/dL", 0, Male(40m, null, text: "> 40"), Female(50m, null, text: "> 50")),
            Num("LDL", "LDL cholesterol", "mg/dL", 0, Any(null, 100m, text: "< 100 (optimal)")),
        ]),
        new("UA", "Serum Uric Acid", "یورک ایسڈ", "Uric acid", "BIOCHEMISTRY", "CHEM", "SERUM", 6,
        [
            Num("UA", "Uric acid", "mg/dL", 1, Male(3.4m, 7.0m), Female(2.4m, 5.7m)),
        ]),
        new("CA", "Serum Calcium", "کیلشیم", "Calcium", "BIOCHEMISTRY", "CHEM", "SERUM", 6,
        [
            Num("CA", "Calcium, total", "mg/dL", 1, Any(8.6m, 10.2m, 6.0m, 13.0m)),
        ]),
        new("CRP", "C-Reactive Protein", "سی آر پی", "CRP", "BIOCHEMISTRY", "CHEM", "SERUM", 6,
        [
            Num("CRP", "CRP", "mg/L", 1, Any(null, 5.0m, text: "< 5")),
        ]),

        // ================= Hormones & vitamins =================
        new("TSH", "Thyroid Stimulating Hormone", "ٹی ایس ایچ", "TSH", "HORMONES", "CHEM", "SERUM", 24,
        [
            Num("TSH", "TSH", "µIU/mL", 2, Any(0.27m, 4.20m)),
        ]),
        new("TFT", "Thyroid Function Tests", "تھائیرائیڈ ٹیسٹ", "TFT", "HORMONES", "CHEM", "SERUM", 24,
        [
            Num("TSH", "TSH", "µIU/mL", 2, Any(0.27m, 4.20m)),
            Num("FT4", "Free T4", "ng/dL", 2, Any(0.93m, 1.70m)),
            Num("FT3", "Free T3", "pg/mL", 2, Any(2.0m, 4.4m)),
        ]),
        new("VITD", "Vitamin D (25-OH)", "وٹامن ڈی", "Vit D", "HORMONES", "CHEM", "SERUM", 48,
        [
            Num("VITD", "25-OH vitamin D", "ng/mL", 1, Any(30m, 100m, text: "30 – 100 (deficient < 20, insufficient 20 – 29)")),
        ]),
        new("B12", "Vitamin B12", "وٹامن بی 12", "B12", "HORMONES", "CHEM", "SERUM", 48,
        [
            Num("B12", "Vitamin B12", "pg/mL", 0, Any(197m, 771m)),
        ]),
        new("FERR", "Serum Ferritin", "فیریٹن", "Ferritin", "HORMONES", "CHEM", "SERUM", 48,
        [
            Num("FERR", "Ferritin", "ng/mL", 1, Male(30m, 400m), Female(13m, 150m)),
        ]),

        // ================= Serology =================
        new("HBSAG", "Hepatitis B Surface Antigen", "ہیپاٹائٹس بی", "HBsAg", "SEROLOGY", "IMMU", "SERUM", 4,
        [
            Reactive("HBSAG", "HBsAg"),
        ]),
        new("HCV", "Anti-HCV Antibodies", "ہیپاٹائٹس سی", "Anti-HCV", "SEROLOGY", "IMMU", "SERUM", 4,
        [
            Reactive("HCV", "Anti-HCV"),
        ]),
        new("HIV", "HIV 1 & 2 Antibodies", "ایچ آئی وی", "HIV", "SEROLOGY", "IMMU", "SERUM", 4,
        [
            Reactive("HIV", "HIV 1 & 2 antibodies"),
        ]),
        new("TYPHI", "Typhidot (Salmonella typhi IgM / IgG)", "ٹائیفی ڈاٹ", "Typhidot", "SEROLOGY", "IMMU", "SERUM", 4,
        [
            Positive("IGM", "S. typhi IgM"),
            Positive("IGG", "S. typhi IgG"),
        ]),
        new("NS1", "Dengue NS1 Antigen", "ڈینگی این ایس ون", "Dengue NS1", "SEROLOGY", "IMMU", "SERUM", 4,
        [
            Positive("NS1", "Dengue NS1 antigen"),
        ]),
        new("UPT", "Pregnancy Test (Urine)", "حمل کا ٹیسٹ", "UPT", "SEROLOGY", "IMMU", "URINE", 1,
        [
            new("UPT", "Urine β-hCG", ResultType.Option, null, 0, null, [new("Negative", "منفی"), new("Positive", "مثبت")], []),
        ]),

        // ================= Urine & stool =================
        new("URINE-RE", "Urine Complete Examination", "پیشاب کا مکمل معائنہ", "Urine R/E", "URINE-STOOL", "CPATH", "URINE", 4,
        [
            Text("COLOUR", "Colour", null, "Physical examination", "Pale yellow"),
            Choice("APPEAR", "Appearance", "Physical examination",
                [new("Clear", "صاف"), new("Slightly turbid", "ہلکا گدلا", true), new("Turbid", "گدلا", true)], "Clear"),
            new("SG", "Specific gravity", ResultType.Numeric, null, 3, "Physical examination", [],
                [new(RangeSex.Any, 1.005m, 1.030m, AllAges: true)]),
            new("PH", "pH", ResultType.Numeric, null, 1, "Chemical examination", [],
                [new(RangeSex.Any, 4.5m, 8.0m, AllAges: true)]),
            Choice("PROT", "Protein", "Chemical examination", NilToPlus3, "Nil"),
            Choice("GLU", "Glucose", "Chemical examination", NilToPlus3, "Nil"),
            Choice("KET", "Ketones", "Chemical examination", NilToPlus3, "Nil"),
            Choice("BLOOD", "Blood", "Chemical examination", NilToPlus3, "Nil"),
            Choice("BIL", "Bilirubin", "Chemical examination", NilToPlus3, "Nil"),
            Choice("UROB", "Urobilinogen", "Chemical examination", [new("Normal", "نارمل"), new("Increased", "زیادہ", true)], "Normal"),
            Choice("NIT", "Nitrite", "Chemical examination", PositiveOptions, "Negative"),
            Text("PUS", "Pus cells", "/HPF", "Microscopic examination", "0 – 5"),
            Text("RBC", "Red blood cells", "/HPF", "Microscopic examination", "0 – 2"),
            Text("EPI", "Epithelial cells", "/HPF", "Microscopic examination", "Few"),
            Text("CASTS", "Casts", "/LPF", "Microscopic examination", "Nil"),
            Text("CRYST", "Crystals", null, "Microscopic examination", "Nil"),
            Text("BACT", "Bacteria", null, "Microscopic examination", "Nil"),
        ]),
        new("STOOL-RE", "Stool Routine Examination", "پاخانے کا معائنہ", "Stool R/E", "URINE-STOOL", "CPATH", "STOOL", 4,
        [
            Text("COLOUR", "Colour", null, "Physical examination", "Brown"),
            Choice("CONS", "Consistency", "Physical examination",
                [new("Formed", "سخت"), new("Semi-formed", "نیم سخت"), new("Loose", "پتلا", true), new("Watery", "پانی جیسا", true)], "Formed"),
            Choice("MUCUS", "Mucus", "Physical examination", [new("Nil", "نہیں"), new("Present", "موجود", true)], "Nil"),
            Choice("BLOOD", "Blood", "Physical examination", [new("Nil", "نہیں"), new("Present", "موجود", true)], "Nil"),
            Text("OVA", "Ova / cysts", null, "Microscopic examination", "Not seen"),
            Text("PUS", "Pus cells", "/HPF", "Microscopic examination", "0 – 2"),
            Text("RBC", "Red blood cells", "/HPF", "Microscopic examination", "Nil"),
        ]),
    ];
}
