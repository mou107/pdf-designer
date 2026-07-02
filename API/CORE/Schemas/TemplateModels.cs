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

        public static readonly string[] All =
        {
            Quote, Invoice, CreditNote, SupplierOrder, OperationSheet, MaintenanceOperationSheet, BonLivraison
        };

        public static bool IsValid(string? docType) => docType != null && All.Contains(docType);
    }

    public class TemplateModel
    {
        public string Id { get; set; } = string.Empty;
        public string? SocieteId { get; set; }
        public string DocType { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public bool IsDefault { get; set; }
        public bool IsActive { get; set; }
        public bool IsSeed { get; set; }
        public int Version { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

    public class CreateTemplateRequest
    {
        public string DocType { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        /// <summary>"seed" (defaut) | "blank" | id d'un template existant a copier.</summary>
        public string From { get; set; } = "seed";
    }

    public class UpdateTemplateRequest
    {
        public string? Name { get; set; }
        public bool? IsActive { get; set; }
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
