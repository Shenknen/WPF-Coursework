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
            return(false, "Такая категория уже существует");

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

    public async Task <(bool Success, string? Error)> RenameAsync(int id, string newName){
        if (string.IsNullOrWhiteSpace(newName))
            return(false, "Название не может быть пустым");
        
        var category = await _db.Categories.FindAsync(id);
        if (category == null)
            return(false, "Категория не найдена");

        var normalized = Category.Normalize(newName);

        var exists = await _db.Categories
            .AnyAsync(c => c.Id != id && c.NormalizedName == normalized);

        if(exists)
            return(false, "Такая категория уже существует");

        category.Rename(newName);

        try
        {
            await _db.SaveChangesAsync();
            return(true, null);
        }
        catch(DbUpdateException)
        {
            return(false, "Такая категория уже существует");
        }
    }
}