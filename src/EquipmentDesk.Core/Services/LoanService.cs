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

    public async Task<(bool Success, string? Error)> ExtendAsync(int loanId, DateTime newDueAtUtc)
    {
        var loan = await _db.Loans.FindAsync(loanId);
        if(loan == null)
            return(false, "Выдача не найдена");
        
        if(loan.ReturnedAtUtc != null)
            return(false, "Нельзя продлить срок, оборудование уже вернули");
        
        if (newDueAtUtc <= DateTime.UtcNow)
            return(false, "Новый срок должен быть позже текущего срока возврата");
        
        var oldDueAtUtc = loan.DueAtUtc;

        loan.DueAtUtc = newDueAtUtc;

        var loanEvent = new LoanEvent
        {
            LoanId = loanId,
            EventType = LoanEventType.DueDateChanged,
            OccurredAtUtc = DateTime.UtcNow,
            OldDueAtUtc = oldDueAtUtc,
            NewDueAtUtc = newDueAtUtc
        };

        _db.LoanEvents.Add(loanEvent);

        await _db.SaveChangesAsync();

        return(true, null);
    }

    public async Task<(bool Success, string? Error)> ReturnAsync(int loanId, bool isBroken, string comment)
    {
        var loan = await _db.Loans.FindAsync(loanId);
        if(loan == null)
            return(false, "Выдача не найдена");
        
        if(loan.ReturnedAtUtc != null)
            return(false, "Эта выдача уже была закрыта ранее");
        
        loan.ReturnedAtUtc = DateTime.UtcNow;
        loan.ReturnCondition = isBroken ? "Неисправен" : "Исправен";
        loan.ReturnComment = comment;

        var loanEvent = new LoanEvent
        {
            LoanId = loanId,
            EventType = LoanEventType.Returned,
            OccurredAtUtc = DateTime.UtcNow,
            Comment = comment
        };
        _db.LoanEvents.Add(loanEvent);

        var item = await _db.EquipmentItems.FindAsync(loan.EquipmentItemId);
        if (item != null)
        {
            if (isBroken)
            {
                item.ServiceState = EquipmentServiceState.Maintenance;
            }
        }

        try
        {
            await _db.SaveChangesAsync();
            return(true,null);
        }
        catch(Exception ex)
        {
            return(false, "Ошибка при сохранении возврата: " + ex.Message);
        }


    }

    public async Task<List<EquipmentLoan>> GetAllAsync()
    {
        return await _db.Loans
        .Include(l => l.EquipmentItem)
        .Include(l => l.Borrower)
        .ToListAsync();
        
    }
}