namespace EquipmentDesk.Core.Data;

/// <summary>
/// Единые пути приложения. Файл БД хранится отдельно от EXE,
/// в %LocalAppData%\EquipmentDesk\ (на Linux — ~/.local/share/EquipmentDesk/).
/// </summary>
public static class AppPaths
{
    public static string DatabaseDirectory { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "EquipmentDesk");

    public static string DatabasePath { get; } = Path.Combine(DatabaseDirectory, "equipment.db");

    public static string ConnectionString => $"Data Source={DatabasePath}";

    public static void EnsureDatabaseDirectory() => Directory.CreateDirectory(DatabaseDirectory);
}
