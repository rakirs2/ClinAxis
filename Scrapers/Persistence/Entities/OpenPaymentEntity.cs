using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Scrapers.Persistence.Entities
{
    public class OpenPaymentEntity
    {
        [Key]
        public long Id { get; set; }
        public Guid InvestigatorPersonId { get; set; }
        public int DataYear { get; set; }
        public string PaymentType { get; set; } = string.Empty;
        public decimal? PaymentAmount { get; set; }
        public DateTime? PaymentDate { get; set; }
        public string? PayorName { get; set; }
        public string? NatureOfPayment { get; set; }
        public string? FormOfPayment { get; set; }
        public string? StudyName { get; set; }
        public string? ClinicalTrialsId { get; set; }
        public string? ContextOfResearch { get; set; }
        public string? ProductCategory { get; set; }
        public string? ProductName { get; set; }
        public string RecordId { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        [ForeignKey(nameof(InvestigatorPersonId))]
        public InvestigatorPersonEntity? InvestigatorPerson { get; set; }
    }
}
