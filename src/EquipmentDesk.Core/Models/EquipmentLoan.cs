namespace EquipmentDesk.Core.Models;

public class EquipmentLoan
{
    public int Id { get; set; }

    public int EquipmentItemId { get; set; }
    public EquipmentItem EquipmentItem { get; set; } = null!;

    public int BorrowerId { get; set; }
    public Borrower Borrower { get; set; } = null!;

    public DateTime IssuedAtUtc { get; set; }
    public DateTime DueAtUtc { get; set; }
    public DateTime? ReturnedAtUtc { get; set; } // null = выдача открыта

    public List<LoanEvent> Events { get; set; } = new();
}