using EquipmentDesk.Core.Data;
using EquipmentDesk.Core.Models;
using Microsoft.EntityFrameworkCore;

namespace EquipmentDesk.Core.Services;

public class BorrowerService
{
    private readonly AppDbContext _db;

    public BorrowerService(AppDbContext db){ _db = db; }
    public async Task<(bool Success, string? Error)> AddAsync(Borrower borrower)
    {
        borrower.FullName = borrower.FullName.Trim();
        borrower.GroupOrDepartment = borrower.GroupOrDepartment.Trim();
        borrower.Contact = borrower.Contact.Trim();

        if (string.IsNullOrWhiteSpace(borrower.FullName))
            return(false, "ФИО не может быть пустым");
        
        if (string.IsNullOrEmpty(borrower.GroupOrDepartment))
            return(false, "Группа или подразделение не могут быть пустыми");

        if (string.IsNullOrEmpty(borrower.Contact))
            return(false, "Контакт не может быть пустым");
        
        borrower.IsActive = true;

        try
        {
            _db.Borrowers.Add(borrower);
            await _db.SaveChangesAsync();
            return(true, null);
        }
        catch(DbUpdateException)
        {
            return(false, "Не удалось добавить получателя");
        }
    }

    public async Task<(bool Success, string? Error)> UpdateAsync(int id, string fullName, string groupOrDepartment, string contact, bool isActive)
    {
        fullName = fullName?.Trim() ?? string.Empty;
        groupOrDepartment = groupOrDepartment?.Trim() ?? string.Empty;
        contact = contact?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(fullName))
            return(false, "ФИО не может быть пустым");

        if (string.IsNullOrWhiteSpace(groupOrDepartment))
            return(false, "Группа или подразделение не могут быть пустыми");

        if (string.IsNullOrWhiteSpace(contact))
            return(false, "Контакт не может быть пустым");

        var borrower = await _db.Borrowers.FindAsync(id);

        if(borrower == null)
            return(false, "Получатель не найден");
        
        borrower.FullName = fullName;
        borrower.GroupOrDepartment = groupOrDepartment;
        borrower.Contact = contact;
        borrower.IsActive = isActive;

        try
        {
            await _db.SaveChangesAsync();
            return(true, null);
        }
        catch(DbUpdateException)
        {
            return(false, "Не удалось изменить получателя");
        }
    }

    public async Task<(bool Success, string? Error)> SetActiveAsync(int id, bool isActive)
    {
        var borrower = await _db.Borrowers.FindAsync(id);
        if(borrower == null)
            return(false, "Получатель не найден");

        borrower.IsActive = isActive;

        try
        {
            await _db.SaveChangesAsync();
            return(false, null);    
        }
        catch(DbUpdateException)
        {
            return(false, "Не удалось активировать/деактивировать выдачу");
        }     
    }

    public async Task<List<Borrower>> GetAllAsync()
    {
        return await _db.Borrowers
            .AsNoTracking()
            .OrderBy(b => b.FullName)
            .ThenBy(b => b.Id)
            .ToListAsync();
    }
}