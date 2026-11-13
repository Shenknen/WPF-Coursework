using System.Windows;
using EquipmentDesk.Core.Data;
using EquipmentDesk.Wpf.ViewModels;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace EquipmentDesk.Wpf;

public partial class App : Application
{
    private ServiceProvider? _services;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        try
        {
            _services = BuildServices();

            await ApplyMigrationsAsync();

            var window = _services.GetRequiredService<MainWindow>();
            MainWindow = window;
            window.Show();
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Не удалось запустить приложение:\n{ex.Message}",
                "Ошибка запуска",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            Shutdown(1);
        }
    }

    private static ServiceProvider BuildServices()
    {
        var services = new ServiceCollection();

        // Один экземпляр фабрики: контекст создаётся на каждую операцию.
        services.AddSingleton<AppDbContextFactory>();
        services.AddSingleton<IDbContextFactory<AppDbContext>>(
            sp => sp.GetRequiredService<AppDbContextFactory>());

        services.AddTransient<MainViewModel>();
        services.AddTransient<MainWindow>();

        return services.BuildServiceProvider();
    }

    private async Task ApplyMigrationsAsync()
    {
        AppPaths.EnsureDatabaseDirectory();

        var factory = _services!.GetRequiredService<IDbContextFactory<AppDbContext>>();
        await using var db = await factory.CreateDbContextAsync();
        await db.Database.MigrateAsync();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _services?.Dispose();
        base.OnExit(e);
    }
}
