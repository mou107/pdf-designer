using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace API.DATABASE.Entities
{
    [Table("report_templates")]
    public class ReportTemplate
    {
        [Key]
        [MaxLength(36)]
        public string Id { get; set; } = Guid.NewGuid().ToString();

        /// <summary>Null = seed global visible par toutes les societes (lecture seule).</summary>
        [MaxLength(36)]
        public string? SocieteId { get; set; }

        /// <summary>Valeurs de TypePdfConfiguration : Quote, Invoice, CreditNote, SupplierOrder, OperationSheet, MaintenanceOperationSheet, BonLivraison.</summary>
        [Required]
        [MaxLength(50)]
        public string DocType { get; set; } = string.Empty;

        [Required]
        [MaxLength(150)]
        public string Name { get; set; } = string.Empty;

        [MaxLength(500)]
        public string? Description { get; set; }

        [Required]
        [MaxLength(500)]
        public string FilePath { get; set; } = string.Empty;

        /// <summary>Numero de modele standard (1-7) dont derive ce template.</summary>
        public int Model { get; set; } = 1;

        /// <summary>Style de tableau (1-3).</summary>
        public int TableStyle { get; set; } = 2;

        /// <summary>Configuration simple (couleurs, colonnes, styles de texte) — JSON injecte en variables au rendu.</summary>
        public string? ConfigJson { get; set; }

        [MaxLength(200)]
        public string? FileNamePattern { get; set; }

        /// <summary>Template personnalise au designer : s'ouvre toujours dans l'editeur avance.</summary>
        public bool IsDesignerCustomized { get; set; }

        /// <summary>Le modele standard d'origine a ete mis a jour par Foliatech (badge passif).</summary>
        public bool MasterUpdateAvailable { get; set; }

        public bool IsDefault { get; set; }

        public bool IsActive { get; set; } = true;

        public int Version { get; set; } = 1;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        [MaxLength(36)]
        public string? CreatedBy { get; set; }

        [MaxLength(36)]
        public string? UpdatedBy { get; set; }

        public ICollection<ReportTemplateVersion> Versions { get; set; } = new List<ReportTemplateVersion>();
    }
}
