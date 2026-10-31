using EquipmentDesk.Core.Data;
using EquipmentDesk.Core.Models;
using Microsoft.EntityFrameworkCore;

namespace EquipmentDesk.Core.Services;

public class CategoryService
{
    private readonly AppDbContext _db;

    //конструктор класса
    public CategoryService(AppDbContext db){ _db = db; }

    public async Task <(bool Success, string? Error)> AddAsync(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return (false, "Название не может быть пустым");

        var exists = await _db.Categories.AnyAsync(c => c.Name.ToLower() == name.Trim().ToLower());
        
        if(exists)
            return(false, "Такая категория уже существует");

        var category = new Category { Name = name.Trim() };
        _db.Categories.Add(category);
        await _db.SaveChangesAsync();

        return(true, null);
    }
}