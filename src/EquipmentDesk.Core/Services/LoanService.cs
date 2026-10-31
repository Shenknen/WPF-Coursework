using EquipmentDesk.Core.Data;
using EquipmentDesk.Core.Models;
using Microsoft.EntityFrameworkCore;

namespace EquipmentDesk.Core.Services;

public class LoanService
{
    private readonly AppDbContext _db;

    public LoanService(AppDbContext db) { _db = db;}

    public async Task<(bool Success, string? Error)> IssueAsync(int equipmentItemId, int borrowerId, DateTime dueAtUtc)
    {
        // валидация : существует ли такой 
        // экземпляр оборудования, получатель(активен ли он?),
        //просрочена ли дата возврата? 

        // отдельно проверить состояние оборудования и 
        // нет ли уже открытой выдачи?

        var item = await _db.EquipmentItems.FindAsync(equipmentItemId);
        if(item == null)
            return(false, "Оборудование не найдено");
        
        if (item.ServiceState != EquipmentServiceState.Operational)
            return (false, "Оборудование неисправно или списано");
        
        var borrower = await _db.Borrowers.FindAsync(borrowerId);
        if(borrower == null || !borrower.IsActive)
            return(false, "Получатель не активен или не найден");
        
        if(dueAtUtc <= DateTime.UtcNow)
            return(false, "Срок возврата должен быть в будущем");
        
        var isAlreadyIssued = await _db.Loans.AnyAsync(l => l.EquipmentItemId == equipmentItemId && l.ReturnedAtUtc == null);
        if(isAlreadyIssued)
            return(false, "Оборудование уже выдано. Сначала оформите возврат");
        
        var loan = new EquipmentLoan
        {
            EquipmentItemId = equipmentItemId,
            BorrowerId = borrowerId,
            IssuedAtUtc = DateTime.UtcNow,
            DueAtUtc = dueAtUtc
        };

        var loanEvent = new LoanEvent
        {
            Loan = loan,
            EventType = LoanEventType.Issued,
            OccurredAtUtc = DateTime.UtcNow
        };

        _db.Loans.Add(loan);
        _db.LoanEvents.Add(loanEvent);

        try
        {
            await _db.SaveChangesAsync();
            return(true, null);
        }
        catch(DbUpdateException)
        {
            return (false, "Конфликт: оборудование было выдано другим пользователем");
        }

    }
}