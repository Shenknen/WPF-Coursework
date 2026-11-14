using EquipmentDesk.Core.Data;
using EquipmentDesk.Core.Models;
using Microsoft.EntityFrameworkCore;

namespace EquipmentDesk.Core.Services;

public class LoanService
{
    private readonly AppDbContext _db;

    public LoanService(AppDbContext db) { _db = db;}

    public async Task<(bool Success, string? Error)> IssueAsync(int equipmentItemId, int borrowerId, DateTime dueAtUtc, string? issueComment = null)
    {
        issueComment = string.IsNullOrWhiteSpace(issueComment) ? null : issueComment.Trim();

        var item = await _db.EquipmentItems.FindAsync(equipmentItemId);
        if(item == null)
            return(false, "Оборудование не найдено");

        if (item.ServiceState != EquipmentServiceState.Operational)
            return (false, "Оборудование неисправно или списано");

        var isAlreadyIssued = await _db.Loans.AnyAsync(
            l => l.EquipmentItemId == equipmentItemId && l.ReturnedAtUtc == null);

        if(isAlreadyIssued)
            return(false, "Оборудование уже выдано");
        
        var borrower = await _db.Borrowers.FindAsync(borrowerId);
        if(borrower == null)
            return(false, "Получатель не найден");
        if(!borrower.IsActive)
            return(false, "Получатель не активен");

        var now = DateTime.UtcNow;

        if(dueAtUtc <= now)
            return(false, "Срок возврата должен быть в будущем");
        
        var loan = new EquipmentLoan
        {
            EquipmentItemId = equipmentItemId,
            BorrowerId = borrowerId,
            IssuedAtUtc = now,
            IssueComment = issueComment,
            DueAtUtc = dueAtUtc
        };

        var loanEvent = new LoanEvent
        {
            Loan = loan,
            EventType = LoanEventType.Issued,
            Comment = issueComment,
            OccurredAtUtc = now
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
            _db.Entry(loanEvent).State = EntityState.Detached;
            _db.Entry(loan).State = EntityState.Detached;
            return (false, "Оборудование уже выдано");
        }
    }

    public async Task<(bool Success, string? Error)> ExtendAsync(int loanId, DateTime newDueAtUtc)
    {
        var now = DateTime.UtcNow;

        var loan = await _db.Loans.FindAsync(loanId);
        if(loan == null)
            return(false, "Выдача не найдена");
        
        if(loan.ReturnedAtUtc != null)
            return(false, "Нельзя продлить срок, оборудование уже вернули");
        
        if (newDueAtUtc <= now)
            return(false, "Новый срок должен быть в будущем");

        if (newDueAtUtc <= loan.DueAtUtc)
            return(false, "Новый срок должен быть позже текущего срока возврата");
        
        var oldDueAtUtc = loan.DueAtUtc;
        loan.DueAtUtc = newDueAtUtc;

        var loanEvent = new LoanEvent
        {
            LoanId = loanId,
            EventType = LoanEventType.DueDateChanged,
            OccurredAtUtc = now,
            OldDueAtUtc = oldDueAtUtc,
            NewDueAtUtc = newDueAtUtc
        };

        _db.LoanEvents.Add(loanEvent);
        try
        {
            await _db.SaveChangesAsync();
            return(true, null);
        }
        catch(DbUpdateException)
        {
            _db.ChangeTracker.Clear();
            return(false, "Не удалось сохранить новый срок");
        }
    }

    public async Task<(bool Success, string? Error)> ReturnAsync(int loanId, bool isBroken, string? comment)
    {
        var loan = await _db.Loans.FindAsync(loanId);
                if(loan == null)
                    return(false, "Выдача не найдена");

        if(loan.ReturnedAtUtc != null)
            return(false, "Эта выдача уже была закрыта ранее");

        comment = string.IsNullOrWhiteSpace(comment) 
            ? null 
            : comment.Trim();
        
        if (isBroken && comment == null)
            return(false, "Опишите неисправность");

        var item = await _db.EquipmentItems.FindAsync(loan.EquipmentItemId);
        if(item == null)
            return(false, "Оборудование не найдено");

        var now = DateTime.UtcNow;
        
        loan.ReturnedAtUtc = now;
        loan.ReturnCondition = isBroken ? "Неисправен" : "Исправен";
        loan.ReturnComment = comment;

        if(isBroken)
            item.ServiceState = EquipmentServiceState.Maintenance;

        var loanEvent = new LoanEvent
        {
            LoanId = loanId,
            EventType = LoanEventType.Returned,
            OccurredAtUtc = now,
            Comment = comment
        };

        _db.LoanEvents.Add(loanEvent);

        try
        {
            await _db.SaveChangesAsync();
            return(true,null);
        }
        catch(DbUpdateException)
        {
            _db.ChangeTracker.Clear();
            return(false, "Не удалось сохранить возврат");
        }
    }

    public async Task<List<EquipmentLoan>> GetAllAsync()
    {
        return await _db.Loans
        .AsNoTracking()
        .Include(l => l.EquipmentItem)
        .Include(l => l.Borrower)
        .OrderBy(l => l.ReturnedAtUtc != null)
        .ThenBy(l => l.DueAtUtc)
        .ThenBy(l => l.Id)
        .ToListAsync();
        
    }
}