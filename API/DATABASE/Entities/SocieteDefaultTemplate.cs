using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace API.DATABASE.Entities
{
    /// <summary>
    /// Pointeur "modele par defaut" par (societe, type de document). Le template cible peut etre
    /// un modele de la societe OU un modele standard global (seed) : une societe peut ainsi definir
    /// un des 7 modeles standards comme defaut SANS avoir a le dupliquer.
    /// Unicite : un seul defaut par (SocieteId, DocType).
    /// </summary>
    [Table("societe_template_defaults")]
    public class SocieteDefaultTemplate
    {
        [Key]
        [MaxLength(36)]
        public string Id { get; set; } = Guid.NewGuid().ToString();

        [Required]
        [MaxLength(36)]
        public string SocieteId { get; set; } = string.Empty;

        [Required]
        [MaxLength(50)]
        public string DocType { get; set; } = string.Empty;

        /// <summary>Id du template par defaut : modele de la societe OU seed global.</summary>
        [Required]
        [MaxLength(36)]
        public string TemplateId { get; set; } = string.Empty;

        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        [MaxLength(36)]
        public string? UpdatedBy { get; set; }
    }
}
