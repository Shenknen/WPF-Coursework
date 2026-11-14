using EquipmentDesk.Core.Data;
using EquipmentDesk.Core.Models;
using EquipmentDesk.Core.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace EquipmentDesk.Tests;

public class LoanServiceTests : IAsyncLifetime
{
    private string _dbPath = string.Empty;
    private AppDbContext _db = null!;
    private LoanService _service = null!;

    public async Task InitializeAsync()
    {
        _dbPath = Path.Combine(
            Path.GetTempPath(),
            "EquipmentDesk.Tests",
            $"{Guid.NewGuid():N}.db");

            Directory.CreateDirectory(Path.GetDirectoryName(_dbPath)!);
            
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite($"Data Source={_dbPath};Pooling=False")
                .Options;

            _db = new AppDbContext(options);
            await _db.Database.MigrateAsync();

            _service = new LoanService(_db);
    }

    public async Task DisposeAsync()
    {
        if (_db is not null)
            await _db.DisposeAsync();

        DeleteIfExist(_dbPath);
        DeleteIfExist(_dbPath + "-wal");
        DeleteIfExist(_dbPath + "-shm");
    }

    private static void DeleteIfExist(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            return;

        try
        {
            File.Delete(path);
        }
        catch(IOException)
        {
            // временный файл не должен ронять тест
        }
    }

    private async Task<Category> AddCategoryAsync(string name = "Тест")
    {
        var category = new Category();
        category.Rename(name);

        _db.Categories.Add(category);
        await _db.SaveChangesAsync();

        return category;
    }

    private async Task<EquipmentItem> AddItemAsync(Category category, string inventoryNumber, EquipmentServiceState state = EquipmentServiceState.Operational)
    {
        var item = new EquipmentItem
        {
            CategoryId = category.Id,
            InventoryNumber = inventoryNumber,
            Name = "Проектор",
            StorageLocation = "Каб. 101",
            ServiceState = state  
        };

        _db.EquipmentItems.Add(item);
        await _db.SaveChangesAsync();

        return item;
    }

    private async Task<Borrower> AddBorrowerAsync(string fullName = "Иванов", bool isActive = true)
    {
        var borrower = new Borrower
        {
            FullName = fullName,
            GroupOrDepartment = "ИС-21",
            Contact = "ivanov@example.com",
            IsActive = isActive
        };

        _db.Borrowers.Add(borrower);
        await _db.SaveChangesAsync();

        return borrower;
    }

    private async Task<EquipmentLoan> AddLoanAsync(EquipmentItem item, Borrower borrower, DateTime issuedAtUtc, DateTime dueAtUtc, DateTime? returnedAtUtc = null)
    {
        var loan = new EquipmentLoan
        {
            EquipmentItemId = item.Id,
            BorrowerId = borrower.Id,
            IssuedAtUtc = issuedAtUtc,
            DueAtUtc = dueAtUtc,
            ReturnedAtUtc = returnedAtUtc
        };

        _db.Loans.Add(loan);
        await _db.SaveChangesAsync();

        return loan;
    }

    private async Task AddEventAsync(EquipmentLoan loan, LoanEventType eventType, DateTime occurredAtUtc, DateTime? oldDueAtUtc = null, DateTime? newDueAtUtc = null, string? comment = null)
    {
        _db.LoanEvents.Add(new LoanEvent
        {
            LoanId = loan.Id,
            EventType = eventType,
            OccurredAtUtc = occurredAtUtc,
            OldDueAtUtc = oldDueAtUtc,
            NewDueAtUtc = newDueAtUtc,
            Comment = comment
        });

        await _db.SaveChangesAsync();
    }

    [Fact]
    public async Task GetOpenAsync_ReturnsOnlyOpenLoans_IncludingOverdue()
    {
        var category = await AddCategoryAsync();
        var borrower = await AddBorrowerAsync();

        var item1 = await AddItemAsync(category, "TEST-01");
        var item2 = await AddItemAsync(category, "TEST-02");
        var item3 = await AddItemAsync(category, "TEST-03");

        var now = DateTime.UtcNow;

        var openFuture = await AddLoanAsync(
            item1, borrower,
            now.AddDays(-1),
            now.AddDays(5));

        var openOverdue = await AddLoanAsync(
            item2, borrower,
            now.AddDays(-10),
            now.AddDays(-2));

        await AddLoanAsync(
            item3, borrower,
            now.AddDays(-10),
            now.AddDays(-5),
            now.AddDays(-4));

        var loans = await _service.GetOpenAsync();

        Assert.Equal(
            new[] { openOverdue.Id, openFuture.Id },
            loans.Select(l => l.Id).ToArray());
    }
    //Здесь проверяется сразу два правила:
    //- закрытая выдача не попадает в список;
    //- просроченная открытая выдача попадает.

    //Закрытые выдачи
    [Fact]
    public async Task GetClosedAsync_ReturnsOnlyClosedLoans()
    {
        var category = await AddCategoryAsync();
        var borrower = await AddBorrowerAsync();

        var item1 = await AddItemAsync(category, "TEST-01");
        var item2 = await AddItemAsync(category, "TEST-02");

        var now = DateTime.UtcNow;

        await AddLoanAsync(
            item1, borrower,
            now.AddDays(-1),
            now.AddDays(5));

        var closed = await AddLoanAsync(
            item2, borrower,
            now.AddDays(-10),
            now.AddDays(-5),
            now.AddDays(-4));

        var loans = await _service.GetClosedAsync();

        var loan = Assert.Single(loans);
        Assert.Equal(closed.Id, loan.Id);
    }
    //Просроченные
    [Fact]
    public async Task GetOverdueAsync_ReturnsOnlyOpenPastDueLoans()
    {
        var category = await AddCategoryAsync();
        var borrower = await AddBorrowerAsync();

        var item1 = await AddItemAsync(category, "TEST-01");
        var item2 = await AddItemAsync(category, "TEST-02");
        var item3 = await AddItemAsync(category, "TEST-03");

        var now = DateTime.UtcNow;

        await AddLoanAsync(
            item1, borrower,
            now.AddDays(-1),
            now.AddDays(5));

        var openOverdue = await AddLoanAsync(
            item2, borrower,
            now.AddDays(-10),
            now.AddDays(-2));

        await AddLoanAsync(
            item3, borrower,
            now.AddDays(-10),
            now.AddDays(-5),
            now.AddDays(-4));

        var loans = await _service.GetOverdueAsync();

        var loan = Assert.Single(loans);
        Assert.Equal(openOverdue.Id, loan.Id);
    }
    //Ключевое: закрытая просроченная выдача не должна попасть в GetOverdueAsync.
    //Счётчики
    [Fact]
    public async Task GetCountsAsync_ReturnsOpenClosedAndOverdueCounts()
    {
        var category = await AddCategoryAsync();
        var borrower = await AddBorrowerAsync();

        var item1 = await AddItemAsync(category, "TEST-01");
        var item2 = await AddItemAsync(category, "TEST-02");
        var item3 = await AddItemAsync(category, "TEST-03");

        var now = DateTime.UtcNow;

        await AddLoanAsync(
            item1, borrower,
            now.AddDays(-1),
            now.AddDays(5));

        await AddLoanAsync(
            item2, borrower,
            now.AddDays(-10),
            now.AddDays(-2));

        await AddLoanAsync(
            item3, borrower,
            now.AddDays(-10),
            now.AddDays(-5),
            now.AddDays(-4));

        var counts = await _service.GetCountsAsync();

        Assert.Equal(2, counts.Open);
        Assert.Equal(1, counts.Closed);
        Assert.Equal(1, counts.Overdue);
    }
    //Обратите внимание: Open = 2, потому что просроченная открытая выдача входит и в открытые, и в просроченные. Overdue — это подмножество Open.
    //Текущая выдача по экземпляру
    [Fact]
    public async Task GetCurrentByItemAsync_ReturnsOpenLoanOrNull()
    {
        var category = await AddCategoryAsync();
        var borrower = await AddBorrowerAsync();

        var itemWithOpen = await AddItemAsync(category, "TEST-01");
        var itemWithoutOpen = await AddItemAsync(category, "TEST-02");

        var now = DateTime.UtcNow;

        await AddLoanAsync(
            itemWithOpen, borrower,
            now.AddDays(-10),
            now.AddDays(-5),
            now.AddDays(-4));

        var open = await AddLoanAsync(
            itemWithOpen, borrower,
            now.AddDays(-1),
            now.AddDays(5));

        await AddLoanAsync(
            itemWithoutOpen, borrower,
            now.AddDays(-10),
            now.AddDays(-5),
            now.AddDays(-4));

        var current = await _service.GetCurrentByItemAsync(itemWithOpen.Id);

        Assert.NotNull(current);
        Assert.Equal(open.Id, current!.Id);
        Assert.Equal(borrower.Id, current.BorrowerId);
        Assert.NotNull(current.Borrower);

        var none = await _service.GetCurrentByItemAsync(itemWithoutOpen.Id);
        Assert.Null(none);
    }
    //События
    [Fact]
    public async Task GetEventsAsync_ReturnsEventsNewestFirst()
    {
        var category = await AddCategoryAsync();
        var borrower = await AddBorrowerAsync();
        var item = await AddItemAsync(category, "TEST-01");

        var now = DateTime.UtcNow;

        var loan = await AddLoanAsync(
            item, borrower,
            now.AddDays(-3),
            now.AddDays(5),
            now.AddDays(-1));

        await AddEventAsync(
            loan, LoanEventType.Issued,
            now.AddDays(-3));

        await AddEventAsync(
            loan, LoanEventType.DueDateChanged,
            now.AddDays(-2),
            now.AddDays(5),
            now.AddDays(10));

        await AddEventAsync(
            loan, LoanEventType.Returned,
            now.AddDays(-1),
            comment: "возврат");

        var events = await _service.GetEventsAsync(loan.Id);

        Assert.Equal(
            new[]
            {
                LoanEventType.Returned,
                LoanEventType.DueDateChanged,
                LoanEventType.Issued
            },
            events.Select(e => e.EventType).ToArray());
    }
    //История по экземпляру
    [Fact]
    public async Task GetHistoryByItemAsync_ReturnsItemLoansAndEventsNewestFirst()
    {
        var category = await AddCategoryAsync();
        var borrower = await AddBorrowerAsync();

        var item = await AddItemAsync(category, "TEST-01");
        var otherItem = await AddItemAsync(category, "TEST-02");

        var now = DateTime.UtcNow;

        var oldLoan = await AddLoanAsync(
            item, borrower,
            now.AddDays(-30),
            now.AddDays(-20),
            now.AddDays(-19));

        var newLoan = await AddLoanAsync(
            item, borrower,
            now.AddDays(-10),
            now.AddDays(5));

        var otherLoan = await AddLoanAsync(
            otherItem, borrower,
            now.AddDays(-5),
            now.AddDays(5));

        await AddEventAsync(
            oldLoan, LoanEventType.Issued,
            now.AddDays(-30));

        await AddEventAsync(
            oldLoan, LoanEventType.Returned,
            now.AddDays(-19));

        await AddEventAsync(
            newLoan, LoanEventType.Issued,
            now.AddDays(-10));

        await AddEventAsync(
            otherLoan, LoanEventType.Issued,
            now.AddDays(-5));

        var history = await _service.GetHistoryByItemAsync(item.Id);

        Assert.Equal(
            new[] { newLoan.Id, oldLoan.Id },
            history.Select(l => l.Id).ToArray());

        Assert.Equal(
            new[]
            {
                LoanEventType.Returned,
                LoanEventType.Issued
            },
            history[1].Events.Select(e => e.EventType).ToArray());

        Assert.Single(history[0].Events);
        Assert.All(history, l => Assert.NotEqual(otherLoan.Id, l.Id));
    }
}