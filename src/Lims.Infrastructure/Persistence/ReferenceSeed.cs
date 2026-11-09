using Lims.Domain.Catalog;

namespace Lims.Infrastructure.Persistence;

/// <summary>
/// Installation-wide reference data seeded by migration. These are units of measure and specimen
/// containers only — no reference ranges, critical limits or interpretations (those must be entered
/// and approved by qualified laboratory staff). IDs are stable: never renumber; append new rows.
/// </summary>
internal static class ReferenceSeed
{
    public static readonly Unit[] Units =
    [
        new() { Id = 1, Code = "g/dL", Name = "grams per decilitre" },
        new() { Id = 2, Code = "g/L", Name = "grams per litre" },
        new() { Id = 3, Code = "mg/dL", Name = "milligrams per decilitre" },
        new() { Id = 4, Code = "mg/L", Name = "milligrams per litre" },
        new() { Id = 5, Code = "µg/dL", Name = "micrograms per decilitre" },
        new() { Id = 6, Code = "µg/L", Name = "micrograms per litre" },
        new() { Id = 7, Code = "ng/mL", Name = "nanograms per millilitre" },
        new() { Id = 8, Code = "ng/dL", Name = "nanograms per decilitre" },
        new() { Id = 9, Code = "pg/mL", Name = "picograms per millilitre" },
        new() { Id = 10, Code = "mmol/L", Name = "millimoles per litre" },
        new() { Id = 11, Code = "µmol/L", Name = "micromoles per litre" },
        new() { Id = 12, Code = "nmol/L", Name = "nanomoles per litre" },
        new() { Id = 13, Code = "pmol/L", Name = "picomoles per litre" },
        new() { Id = 14, Code = "mEq/L", Name = "milliequivalents per litre" },
        new() { Id = 15, Code = "U/L", Name = "units per litre" },
        new() { Id = 16, Code = "IU/L", Name = "international units per litre" },
        new() { Id = 17, Code = "mIU/mL", Name = "milli-international units per millilitre" },
        new() { Id = 18, Code = "µIU/mL", Name = "micro-international units per millilitre" },
        new() { Id = 19, Code = "IU/mL", Name = "international units per millilitre" },
        new() { Id = 20, Code = "%", Name = "percent" },
        new() { Id = 21, Code = "fL", Name = "femtolitres" },
        new() { Id = 22, Code = "pg", Name = "picograms" },
        new() { Id = 23, Code = "×10³/µL", Name = "thousand per microlitre" },
        new() { Id = 24, Code = "×10⁶/µL", Name = "million per microlitre" },
        new() { Id = 25, Code = "×10⁹/L", Name = "10⁹ per litre" },
        new() { Id = 26, Code = "×10¹²/L", Name = "10¹² per litre" },
        new() { Id = 27, Code = "cells/µL", Name = "cells per microlitre" },
        new() { Id = 28, Code = "/HPF", Name = "per high-power field" },
        new() { Id = 29, Code = "/LPF", Name = "per low-power field" },
        new() { Id = 30, Code = "mm/hr", Name = "millimetres per hour" },
        new() { Id = 31, Code = "sec", Name = "seconds" },
        new() { Id = 32, Code = "min", Name = "minutes" },
        new() { Id = 33, Code = "ratio", Name = "ratio (dimensionless)" },
        new() { Id = 34, Code = "mL/min/1.73m²", Name = "millilitres per minute per 1.73 m²" },
        new() { Id = 35, Code = "mOsm/kg", Name = "milliosmoles per kilogram" },
        new() { Id = 36, Code = "copies/mL", Name = "copies per millilitre" },
        new() { Id = 37, Code = "titre", Name = "titre" },
        new() { Id = 38, Code = "mL", Name = "millilitres" },
        new() { Id = 39, Code = "g/24h", Name = "grams per 24 hours" },
        new() { Id = 40, Code = "mg/24h", Name = "milligrams per 24 hours" },
        new() { Id = 41, Code = "S/CO", Name = "signal to cut-off ratio" },
        new() { Id = 42, Code = "COI", Name = "cut-off index" },
    ];

    public static readonly SampleType[] SampleTypes =
    [
        new() { Id = 1, Code = "WB-EDTA", NameEn = "Whole blood (EDTA)", NameUr = "خون (EDTA)", Container = "EDTA tube", CapColour = "Lavender" },
        new() { Id = 2, Code = "SERUM", NameEn = "Serum", NameUr = "سیرم", Container = "Clot activator / gel tube", CapColour = "Red / Yellow" },
        new() { Id = 3, Code = "PL-CIT", NameEn = "Plasma (sodium citrate)", NameUr = "پلازما (سوڈیم سائٹریٹ)", Container = "Sodium citrate tube", CapColour = "Light blue" },
        new() { Id = 4, Code = "PL-FLU", NameEn = "Plasma (sodium fluoride)", NameUr = "پلازما (سوڈیم فلورائیڈ)", Container = "Fluoride oxalate tube", CapColour = "Grey" },
        new() { Id = 5, Code = "PL-HEP", NameEn = "Plasma (heparin)", NameUr = "پلازما (ہیپرین)", Container = "Lithium heparin tube", CapColour = "Green" },
        new() { Id = 6, Code = "WB-HEP", NameEn = "Whole blood (heparin)", NameUr = "خون (ہیپرین)", Container = "Heparin tube / syringe", CapColour = "Green" },
        new() { Id = 7, Code = "URINE", NameEn = "Urine (random)", NameUr = "پیشاب", Container = "Sterile urine container", CapColour = null },
        new() { Id = 8, Code = "URINE-24H", NameEn = "Urine (24-hour)", NameUr = "پیشاب (24 گھنٹے)", Container = "24-hour urine container", CapColour = null },
        new() { Id = 9, Code = "STOOL", NameEn = "Stool", NameUr = "پاخانہ", Container = "Stool container", CapColour = null },
        new() { Id = 10, Code = "CSF", NameEn = "Cerebrospinal fluid", NameUr = "دماغی رطوبت (CSF)", Container = "Sterile tube", CapColour = null },
        new() { Id = 11, Code = "SPUTUM", NameEn = "Sputum", NameUr = "بلغم", Container = "Sterile container", CapColour = null },
        new() { Id = 12, Code = "SWAB", NameEn = "Swab", NameUr = "سواب", Container = "Swab with transport medium", CapColour = null },
        new() { Id = 13, Code = "SEMEN", NameEn = "Semen", NameUr = "منی", Container = "Sterile container", CapColour = null },
        new() { Id = 14, Code = "FLUID", NameEn = "Body fluid", NameUr = "جسمانی رطوبت", Container = "Sterile container", CapColour = null },
        new() { Id = 15, Code = "TISSUE", NameEn = "Tissue (biopsy)", NameUr = "ٹشو (بائیوپسی)", Container = "Formalin container", CapColour = null },
        new() { Id = 16, Code = "BLOOD-CULT", NameEn = "Blood (culture)", NameUr = "خون (کلچر)", Container = "Blood culture bottle", CapColour = null },
    ];
}
