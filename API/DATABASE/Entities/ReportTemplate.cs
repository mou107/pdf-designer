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

        [Required]
        [MaxLength(500)]
        public string FilePath { get; set; } = string.Empty;

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
