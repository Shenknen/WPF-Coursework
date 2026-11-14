using System.Runtime.ExceptionServices;
using EquipmentDesk.Core.Data;
using EquipmentDesk.Core.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace EquipmentDesk.Core.Services;

public class EquipmentService
{
    private readonly AppDbContext _db;

    public EquipmentService(AppDbContext db) { _db = db; }

    public async Task<(bool Success, string? Error)> AddAsync(EquipmentItem item)
    {
        item.Name = item.Name.Trim();
        item.InventoryNumber = EquipmentItem.NormalizeInventoryNumber(item.InventoryNumber);
        item.StorageLocation = item.StorageLocation.Trim();
        item.SerialNumber = string.IsNullOrWhiteSpace(item.SerialNumber) 
            ? null 
            : item.SerialNumber.Trim();

        //блок валидации
        if (string.IsNullOrWhiteSpace(item.Name))
            return(false, "Название оборудования не может быть пустым");

        if (string.IsNullOrWhiteSpace(item.InventoryNumber))
            return(false, "Инвентарный номер не может быть пустым");
        
        if (string.IsNullOrWhiteSpace(item.StorageLocation))
            return(false, "Место хранения не может быть пустым");

        var categoryExists = await _db.Categories.AnyAsync(c => c.Id == item.CategoryId);
        if (!categoryExists)
            return (false, "Выбранная категория не найдена");
        
        var duplicate = await _db.EquipmentItems
            .AnyAsync(e => e.InventoryNumber == item.InventoryNumber);

        if(duplicate)
            return(false, $"Оборудование с номером '{item.InventoryNumber}' уже существует");
        
        item.ServiceState = EquipmentServiceState.Operational;

        try
        {
            _db.EquipmentItems.Add(item);
            await _db.SaveChangesAsync();
            return(true, null);
        }
        catch (DbUpdateException)
        {
            _db.Entry(item).State = EntityState.Detached;
            return(false, $"Оборудование с номером '{item.InventoryNumber}' уже существует");
        }
    }

    public async Task<(bool Success, string? Error)> UpdateAsync(int id, int categoryId, string inventoryNumber, string name, string? serialNumber, string storageLocation)
    {
        inventoryNumber = EquipmentItem.NormalizeInventoryNumber(inventoryNumber);
        name = name?.Trim()  ?? string.Empty;
        storageLocation = storageLocation?.Trim() ?? string.Empty;
        serialNumber = string.IsNullOrWhiteSpace(serialNumber)
            ? null 
            : serialNumber.Trim();
        
        if (string.IsNullOrWhiteSpace(name))
            return(false, "Категория не может быть пустой");
        
        if (string.IsNullOrWhiteSpace(inventoryNumber))
            return(false, "Инвентарный номер не может быть пустым");

        if (string.IsNullOrWhiteSpace(storageLocation))
            return(false, "Место хранения не может быть пустым");

        var categoryExists = await _db.Categories.AnyAsync(c => c.Id == categoryId);
        if (!categoryExists)
            return(false, "Выбранная категория не найдена");

        var item = await _db.EquipmentItems.FindAsync(id);
        if (item == null)
            return(false, "Оборудование не найдено");
        
        var duplicate = await _db.EquipmentItems
            .AnyAsync(e => e.Id != id && e.InventoryNumber == inventoryNumber);
        
        item.CategoryId = categoryId;
        item.InventoryNumber = inventoryNumber;
        item.Name = name;
        item.SerialNumber = serialNumber;
        item.StorageLocation = storageLocation;

        try
        {
            await _db.SaveChangesAsync();
            return(true, null);
        }
        catch
        {
            _db.ChangeTracker.Clear();
            return(false, $"Оборудование с номером '{inventoryNumber}' уже существует");
        }
    }
}