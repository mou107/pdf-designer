using System.Text.Json;

namespace API.CORE.Schemas
{
    /// <summary>Types de documents supportes — alignes sur TypePdfConfiguration du webapi Axiobat.</summary>
    public static class DocTypes
    {
        public const string Quote = "Quote";
        public const string Invoice = "Invoice";
        public const string CreditNote = "CreditNote";
        public const string SupplierOrder = "SupplierOrder";
        public const string OperationSheet = "OperationSheet";
        public const string MaintenanceOperationSheet = "MaintenanceOperationSheet";
        public const string BonLivraison = "BonLivraison";

        // Nouveaux documents (sans equivalent pdfmake) : un seul modele standard chacun.
        public const string WorksiteSheet = "WorksiteSheet";       // Fiche chantier
        public const string CustomerSheet = "CustomerSheet";       // Fiche client / prospect
        public const string DealSheet = "DealSheet";               // Fiche affaire
        public const string TimeSheet = "TimeSheet";               // Releve d'heures

        public static readonly string[] All =
        {
            Quote, Invoice, CreditNote, SupplierOrder, OperationSheet, MaintenanceOperationSheet, BonLivraison,
            WorksiteSheet, CustomerSheet, DealSheet, TimeSheet
        };

        /// <summary>Nouveaux documents : un seul modele standard (pas de matrice 1-7, pas de migration).</summary>
        public static readonly string[] SingleModel = { WorksiteSheet, CustomerSheet, DealSheet, TimeSheet };

        public static bool IsValid(string? docType) => docType != null && All.Contains(docType);
    }

    public class TemplateModel
    {
        public string Id { get; set; } = string.Empty;
        public string? SocieteId { get; set; }
        public string DocType { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public bool IsDefault { get; set; }
        public bool IsActive { get; set; }
        public bool IsSeed { get; set; }
        /// <summary>Numero de modele standard (1-7) dont derive ce template.</summary>
        public int Model { get; set; } = 1;
        /// <summary>Style de tableau (1-3).</summary>
        public int TableStyle { get; set; } = 2;
        /// <summary>Configuration simple (couleurs, colonnes, styles de texte) — JSON injecte en variables au rendu.</summary>
        public string? ConfigJson { get; set; }
        public string? FileNamePattern { get; set; }
        /// <summary>Template edite dans le designer : s'ouvre toujours dans l'editeur avance, config simple desactivee.</summary>
        public bool IsDesignerCustomized { get; set; }
        /// <summary>Le modele standard d'origine a ete mis a jour par Foliatech (badge "Mise a jour disponible").</summary>
        public bool MasterUpdateAvailable { get; set; }
        public int Version { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public string? CreatedBy { get; set; }
        public string? UpdatedBy { get; set; }
    }

    public class CreateTemplateRequest
    {
        public string DocType { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        /// <summary>"seed" (defaut) | "blank" | id d'un template existant a copier.</summary>
        public string From { get; set; } = "seed";
        /// <summary>Numero de modele standard de depart (1-7).</summary>
        public int Model { get; set; } = 1;
    }

    /// <summary>Mise a jour des metadonnees (nom, description, nom de fichier) — ne bump pas la version.</summary>
    public class UpdateTemplateRequest
    {
        public string? Name { get; set; }
        public string? Description { get; set; }
        public string? FileNamePattern { get; set; }
        public bool? IsActive { get; set; }
    }

    /// <summary>Sauvegarde de la configuration simple depuis l'ecran de parametrage — cree une nouvelle version.</summary>
    public class SaveConfigRequest
    {
        public int? Model { get; set; }
        public int? TableStyle { get; set; }
        public string? ConfigJson { get; set; }
        public string? FileNamePattern { get; set; }
    }

    /// <summary>Enveloppe versionnee envoyee par les clients web/mobile a api/render.</summary>
    public class PdfRenderPayload
    {
        public int SchemaVersion { get; set; } = 1;
        public string DocType { get; set; } = string.Empty;
        public string? TemplateId { get; set; }
        public JsonElement? Document { get; set; }
        public JsonElement? Societe { get; set; }
        public JsonElement? Options { get; set; }
    }
}
