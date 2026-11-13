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

        var normalized = Category.Normalize(name);

        if (await _db.Categories.AnyAsync(c => c.NormalizedName == normalized))
            return(false, "Название категории не может быть пустым");

        var category = new Category();
        category.Rename(name);

        _db.Categories.Add(category);

        try
        {
            await _db.SaveChangesAsync();
            return (true, null);
        }
        catch(DbUpdateException)
        {
            return(false, "Такая категория уже существует");
        }
    }
}