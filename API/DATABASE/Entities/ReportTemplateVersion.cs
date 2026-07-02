using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace API.DATABASE.Entities
{
    [Table("report_template_versions")]
    public class ReportTemplateVersion
    {
        [Key]
        [MaxLength(36)]
        public string Id { get; set; } = Guid.NewGuid().ToString();

        [Required]
        [MaxLength(36)]
        public string TemplateId { get; set; } = string.Empty;

        public int Version { get; set; }

        [Required]
        [MaxLength(500)]
        public string FilePath { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [MaxLength(36)]
        public string? CreatedBy { get; set; }

        [ForeignKey(nameof(TemplateId))]
        public ReportTemplate? Template { get; set; }
    }
}
