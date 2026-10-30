namespace EquipmentDesk.Core.Models;

public class LoanEvent
{
    public int Id { get; set; }

    public int LoanId { get; set; }
    public EquipmentLoan Loan { get; set; } = null!;

    public LoanEventType EventType { get; set; }
    public DateTime OccurredAtUtc { get; set; }

    public DateTime? OldDueAtUtc { get; set; }
    public DateTime? NewDueAtUtc { get; set; }

    public string? Comment { get; set;}
}