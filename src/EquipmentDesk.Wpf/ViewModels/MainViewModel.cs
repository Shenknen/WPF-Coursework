using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EquipmentDesk.Core.Data;
using Microsoft.EntityFrameworkCore;

namespace EquipmentDesk.Wpf.ViewModels;

/// <summary>
/// Временная ViewModel этапа 2: проверяет, что DI, БД и привязки работают.
/// Навигация и экраны появятся на следующих этапах.
/// </summary>
public partial class MainViewModel : ObservableObject
{
    private readonly IDbContextFactory<AppDbContext> _dbFactory;

    [ObservableProperty]
    private string _status = "Проверка базы данных…";

    public MainViewModel(IDbContextFactory<AppDbContext> dbFactory)
    {
        _dbFactory = dbFactory;
        _ = RefreshAsync();
    }

    public string DatabasePath => AppPaths.DatabasePath;

    [RelayCommand]
    private async Task RefreshAsync()
    {
        try
        {
            await using var db = await _dbFactory.CreateDbContextAsync();

            var items = await db.EquipmentItems.CountAsync();
            var openLoans = await db.Loans.CountAsync(l => l.ReturnedAtUtc == null);

            Status = $"База данных доступна. Экземпляров: {items}, открытых выдач: {openLoans}.";
        }
        catch (Exception ex)
        {
            Status = $"Ошибка доступа к БД: {ex.Message}";
        }
    }
}
