using EquipmentDesk.Core.Data;
using EquipmentDesk.Core.Models;
using Microsoft.EntityFrameworkCore;


public class EquipmentService
{
    private readonly AppDbContext _db;

    public EquipmentService(AppDbContext db) { _db = db; }

    public async Task<(bool Success, string? Error)> AddAsync(EquipmentItem item)
    {
        //блок валидации
        if (string.IsNullOrWhiteSpace(item.Name))
            return(false, "Название оборудования не может быть пустым");

        if (string.IsNullOrWhiteSpace(item.InventoryNumber))
            return(false, "Инвентарный номер не может быть пустым");
        
        var categoryExists = await _db.Categories.AnyAsync(c => c.Id == item.CategoryId);
        if (!categoryExists)
            return (false, "Выбранная категория не найдена");
        
        var duplicate = await _db.EquipmentItems
        .AnyAsync(e => e.InventoryNumber.ToLower() == item.InventoryNumber.ToLower());

        if(duplicate)
            return(false, $"Оборудование с номером '{item.InventoryNumber}' уже существует");
        
        item.Name = item.Name.Trim();
        item.InventoryNumber = item.InventoryNumber.Trim();

        _db.EquipmentItems.Add(item);
        await _db.SaveChangesAsync();

        return (true, null);
    }
}