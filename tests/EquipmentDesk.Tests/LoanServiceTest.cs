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
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite("Data Source=:memory")
            .Options;
        
        using var db = new AppDbContext(options);
        await db.Database.EnsureCreatedAsync();

        var category = new Category { Name = "Тест" };
        var item = new EquipmentItem { Name = "Проектор", InventoryNumber = "TEST-01", Category = category };
        var borrower = new Borrower { FullName = "Иванов", IsActive = true};

        db.EquipmentItems.Add(item);
        db.Borrowers.Add(borrower);
        await db.SaveChangesAsync();

        var service = new LoanService(db);
        var result = await service.IssueAsync(item.Id, borrower.Id, DateTime.UtcNow.AddDays(1));

        Assert.True(result.Success);
        Assert.Null(result.Error);

        var loan = await db.Loans.FirstOrDefaultAsync();
        Assert.NotNull(loan);
        Assert.Null(loan.ReturnedAtUtc);
    }
}