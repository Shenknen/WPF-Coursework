using EquipmentDesk.Core.Data;
using EquipmentDesk.Core.Models;
using EquipmentDesk.Core.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace EquipmentDesk.Tests;

public class EquipmentServiceTests : IAsyncLifetime
{
    private string _dbPath = string.Empty;
    private AppDbContext _db = null!;
    private EquipmentService _service = null!;

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

        _service = new EquipmentService(_db);
    }

    public async Task DisposeAsync()
    {
        if (_db is not null)
            await _db.DisposeAsync();

        DeleteIfExists(_dbPath);
        DeleteIfExists(_dbPath + "-wal");
        DeleteIfExists(_dbPath + "-shm");
    }

    private static void DeleteIfExists(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            return;

        try
        {
            File.Delete(path);
        }
        catch (IOException)
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

    private async Task<EquipmentItem> AddItemAsync(
        Category category,
        string inventoryNumber,
        string name = "Проектор",
        EquipmentServiceState state = EquipmentServiceState.Operational)
    {
        var item = new EquipmentItem
        {
            CategoryId = category.Id,
            InventoryNumber = inventoryNumber,
            Name = name,
            StorageLocation = "Каб. 101",
            ServiceState = state
        };

        _db.EquipmentItems.Add(item);
        await _db.SaveChangesAsync();

        return item;
    }

    private async Task AddOpenLoanAsync(EquipmentItem item)
    {
        var borrower = new Borrower
        {
            FullName = "Иванов",
            GroupOrDepartment = "ИС-21",
            Contact = "ivanov@example.com",
            IsActive = true
        };

        _db.Borrowers.Add(borrower);
        await _db.SaveChangesAsync();

        var now = DateTime.UtcNow;

        _db.Loans.Add(new EquipmentLoan
        {
            EquipmentItemId = item.Id,
            BorrowerId = borrower.Id,
            IssuedAtUtc = now.AddDays(-1),
            DueAtUtc = now.AddDays(1)
        });

        await _db.SaveChangesAsync();
    }

    [Fact]
    public async Task AddAsync_NormalizesFields_AndForcesOperational()
    {
        var category = await AddCategoryAsync();

        var item = new EquipmentItem
        {
            CategoryId = category.Id,
            InventoryNumber = "  inv   001  ",
            Name = "  Проектор  ",
            StorageLocation = "  Каб. 101  ",
            SerialNumber = "   ",
            ServiceState = EquipmentServiceState.Retired
        };

        var result = await _service.AddAsync(item);

        Assert.True(result.Success);
        Assert.Null(result.Error);

        _db.ChangeTracker.Clear();

        var saved = await _db.EquipmentItems.SingleAsync();
        Assert.Equal("INV 001", saved.InventoryNumber);
        Assert.Equal("Проектор", saved.Name);
        Assert.Equal("Каб. 101", saved.StorageLocation);
        Assert.Null(saved.SerialNumber);
        Assert.Equal(EquipmentServiceState.Operational, saved.ServiceState);
    }

    [Fact]
    public async Task AddAsync_WhenDuplicateInventoryNumber_ReturnsError()
    {
        var category = await AddCategoryAsync();
        await AddItemAsync(category, "INV-01");

        var duplicate = new EquipmentItem
        {
            CategoryId = category.Id,
            InventoryNumber = "  inv-01  ",
            Name = "Другой проектор",
            StorageLocation = "Каб. 102"
        };

        var result = await _service.AddAsync(duplicate);

        Assert.False(result.Success);
        Assert.Equal("Оборудование с номером 'INV-01' уже существует", result.Error);
        Assert.Equal(1, await _db.EquipmentItems.CountAsync());
    }

    [Theory]
    [InlineData("", "INV-01", "Каб. 101", "Название оборудования не может быть пустым")]
    [InlineData("Проектор", "   ", "Каб. 101", "Инвентарный номер не может быть пустым")]
    [InlineData("Проектор", "INV-01", "   ", "Место хранения не может быть пустым")]
    public async Task AddAsync_WhenRequiredFieldIsEmpty_ReturnsError(
        string name,
        string inventoryNumber,
        string storageLocation,
        string expectedError)
    {
        var category = await AddCategoryAsync();

        var item = new EquipmentItem
        {
            CategoryId = category.Id,
            Name = name,
            InventoryNumber = inventoryNumber,
            StorageLocation = storageLocation
        };

        var result = await _service.AddAsync(item);

        Assert.False(result.Success);
        Assert.Equal(expectedError, result.Error);
        Assert.Empty(await _db.EquipmentItems.ToListAsync());
    }

    [Fact]
    public async Task AddAsync_WhenCategoryMissing_ReturnsError()
    {
        var item = new EquipmentItem
        {
            CategoryId = 999,
            InventoryNumber = "INV-01",
            Name = "Проектор",
            StorageLocation = "Каб. 101"
        };

        var result = await _service.AddAsync(item);

        Assert.False(result.Success);
        Assert.Equal("Выбранная категория не найдена", result.Error);
        Assert.Empty(await _db.EquipmentItems.ToListAsync());
    }

    [Fact]
    public async Task UpdateAsync_WhenDataValid_UpdatesFieldsAndNormalizesInventoryNumber()
    {
        var category = await AddCategoryAsync();
        var item = await AddItemAsync(category, "OLD-01");

        var newCategory = await AddCategoryAsync("Новая категория");

        var result = await _service.UpdateAsync(
            item.Id,
            newCategory.Id,
            "  new   002  ",
            "  Новый проектор  ",
            "  SN-123  ",
            "  Каб. 202  ");

        Assert.True(result.Success);
        Assert.Null(result.Error);

        _db.ChangeTracker.Clear();

        var saved = await _db.EquipmentItems.SingleAsync(i => i.Id == item.Id);
        Assert.Equal(newCategory.Id, saved.CategoryId);
        Assert.Equal("NEW 002", saved.InventoryNumber);
        Assert.Equal("Новый проектор", saved.Name);
        Assert.Equal("SN-123", saved.SerialNumber);
        Assert.Equal("Каб. 202", saved.StorageLocation);
    }

    [Fact]
    public async Task UpdateAsync_WhenNameEmpty_ReturnsNameError()
    {
        var category = await AddCategoryAsync();
        var item = await AddItemAsync(category, "INV-01");

        var result = await _service.UpdateAsync(
            item.Id,
            category.Id,
            "INV-02",
            "   ",
            null,
            "Каб. 101");

        Assert.False(result.Success);
        Assert.Equal("Название оборудования не может быть пустым", result.Error);

        _db.ChangeTracker.Clear();

        var saved = await _db.EquipmentItems.SingleAsync(i => i.Id == item.Id);
        Assert.Equal("INV-01", saved.InventoryNumber);
        Assert.Equal("Проектор", saved.Name);
    }

    [Fact]
    public async Task UpdateAsync_WhenDuplicateInventoryNumber_ReturnsError()
    {
        var category = await AddCategoryAsync();
        await AddItemAsync(category, "INV-01");
        var second = await AddItemAsync(category, "INV-02", "Второй проектор");

        var result = await _service.UpdateAsync(
            second.Id,
            category.Id,
            "  inv-01  ",
            "Второй проектор",
            null,
            "Каб. 102");

        Assert.False(result.Success);
        Assert.Equal("Оборудование с номером 'INV-01' уже существует", result.Error);

        _db.ChangeTracker.Clear();

        var saved = await _db.EquipmentItems.SingleAsync(i => i.Id == second.Id);
        Assert.Equal("INV-02", saved.InventoryNumber);
    }

    [Fact]
    public async Task UpdateAsync_WhenItemMissing_ReturnsError()
    {
        var category = await AddCategoryAsync();

        var result = await _service.UpdateAsync(
            999,
            category.Id,
            "INV-01",
            "Проектор",
            null,
            "Каб. 101");

        Assert.False(result.Success);
        Assert.Equal("Оборудование не найдено", result.Error);
    }

    [Fact]
    public async Task ChangeStateAsync_WhenItemHasOpenLoan_ReturnsError()
    {
        var category = await AddCategoryAsync();
        var item = await AddItemAsync(category, "INV-01");
        await AddOpenLoanAsync(item);

        var result = await _service.ChangeStateAsync(
            item.Id,
            EquipmentServiceState.Maintenance);

        Assert.False(result.Success);
        Assert.Equal(
            "Нельзя изменить состояние выбранного оборудования. Сначала оформите возврат",
            result.Error);

        _db.ChangeTracker.Clear();

        var saved = await _db.EquipmentItems.SingleAsync(i => i.Id == item.Id);
        Assert.Equal(EquipmentServiceState.Operational, saved.ServiceState);
    }

    [Fact]
    public async Task ChangeStateAsync_WhenItemRetired_ReturnsError()
    {
        var category = await AddCategoryAsync();
        var item = await AddItemAsync(
            category,
            "INV-01",
            state: EquipmentServiceState.Retired);

        var result = await _service.ChangeStateAsync(
            item.Id,
            EquipmentServiceState.Operational);

        Assert.False(result.Success);
        Assert.Equal("Списанное оборудование нельзя изменить", result.Error);

        _db.ChangeTracker.Clear();

        var saved = await _db.EquipmentItems.SingleAsync(i => i.Id == item.Id);
        Assert.Equal(EquipmentServiceState.Retired, saved.ServiceState);
    }

    [Fact]
    public async Task ChangeStateAsync_WhenNoOpenLoan_ChangesState()
    {
        var category = await AddCategoryAsync();
        var item = await AddItemAsync(category, "INV-01");

        var result = await _service.ChangeStateAsync(
            item.Id,
            EquipmentServiceState.Maintenance);

        Assert.True(result.Success);
        Assert.Null(result.Error);

        _db.ChangeTracker.Clear();

        var saved = await _db.EquipmentItems.SingleAsync(i => i.Id == item.Id);
        Assert.Equal(EquipmentServiceState.Maintenance, saved.ServiceState);
    }

    [Fact]
    public async Task GetAllAsync_ReturnsItemsWithCategoryAndStableOrder()
    {
        var category = await AddCategoryAsync();
        await AddItemAsync(category, "INV-02", "Второй");
        await AddItemAsync(category, "INV-01", "Первый");

        var items = await _service.GetAllAsync();

        Assert.Equal(
            new[] { "INV-01", "INV-02" },
            items.Select(i => i.InventoryNumber).ToArray());

        Assert.All(items, item => Assert.NotNull(item.Category));
    }
}
