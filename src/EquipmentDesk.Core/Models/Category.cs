namespace EquipmentDesk.Core.Models;

public class Category
{
    public int Id { get; set; }

    // Название которое видит пользователь
    public string Name { get; set; } = string.Empty; 

    public string NormalizedName { get; set; } = string.Empty; 

    public ICollection<EquipmentItem> Items { get; set; } = new List<EquipmentItem>();

    public static string Normalize(string value) =>
        string.Join(' ', value.Split(
            (char[]?)null,
            StringSplitOptions.RemoveEmptyEntries)).ToUpperInvariant();
    
    public void Rename(string name)
    {
        var trimmed = name.Trim();
        Name = trimmed;
        NormalizedName = Normalize(trimmed);
    }
}