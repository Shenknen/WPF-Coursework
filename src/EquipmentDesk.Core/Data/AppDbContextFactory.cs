using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace EquipmentDesk.Core.Data;

/// <summary>
/// Создаёт контекст на одну законченную операцию.
/// Используется и EF-инструментами (dotnet ef), и приложением через DI.
/// </summary>
public class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>, IDbContextFactory<AppDbContext>
{
    private readonly DbContextOptions<AppDbContext> _options;

    // Конструктор без параметров нужен инструментам dotnet ef.
    public AppDbContextFactory() : this(AppPaths.ConnectionString)
    {
        AppPaths.EnsureDatabaseDirectory();
    }

    public AppDbContextFactory(string connectionString)
    {
        _options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connectionString)
            .Options;
    }

    public AppDbContext CreateDbContext() => new(_options);

    AppDbContext IDesignTimeDbContextFactory<AppDbContext>.CreateDbContext(string[] args) => CreateDbContext();
}

