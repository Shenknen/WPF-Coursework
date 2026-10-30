namespace EquipmentDesk.Core.Models;

public class EquipmentItem
{
    public int Id { get; set; }
    public int CategoryId { get; set; }
    public Category Category { get; set; } = null!;

    public string InventoryNumber { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? SerialNumber { get; set; }
    public string StorageLocation { get; set; } = string.Empty;

    public EquipmentServiceState ServiceState { get; set; } = EquipmentServiceState.Operational;

}