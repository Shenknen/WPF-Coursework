using EquipmentDesk.Core.Data;
using EquipmentDesk.Core.Models;
using EquipmentDesk.Core.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace EquipmentDesk.Tests;

public class LoanServiceTests
{
    [Fact]
    public async Task Issue_WhenItemIsFree_ReturnsSuccess()
    {
        var path = Path.Combine(
            Path.GetTempPath(),
            "EquipmentDesk.Tests",
            $"{Guid.NewGuid():N}.db");

        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        try
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite($"Data Source={path};Pooling=False")
                .Options;

            await using var db = new AppDbContext(options);
            await db.Database.MigrateAsync();

            var category = new Category();
            category.Rename("Тест");

            var item = new EquipmentItem
            {
                Name = "Проектор",
                InventoryNumber = "TEST-01",
                StorageLocation = "Каб. 101",
                Category = category
            };

            var borrower = new Borrower
            {
                FullName = "Иванов",
                GroupOrDepartment = "ИС-21",
                Contact = "ivanov@example.com",
                IsActive = true
            };

            db.EquipmentItems.Add(item);
            db.Borrowers.Add(borrower);
            await db.SaveChangesAsync();

            var service = new LoanService(db);

            var result = await service.IssueAsync(
                item.Id,
                borrower.Id,
                DateTime.UtcNow.AddDays(1));
            
            Assert.True(result.Success);
            Assert.Null(result.Error);

            var loan = await db.Loans.SingleAsync();
            Assert.Null(loan.ReturnedAtUtc);

            var loanEvent = await db.LoanEvents.SingleAsync();
            Assert.Equal(LoanEventType.Issued, loanEvent.EventType);
        }
        finally
        {
            DeleteIfExists(path);
            DeleteIfExists(path + "-wal");
            DeleteIfExists(path + "-shm");
        }
    }

    private static void DeleteIfExists(string path)
    {
        try
        {
            if(File.Exists(path))
                File.Delete(path);
        }
        catch (IOException)
        {
            // временный файл не должен ронять тест
        }
    }
}