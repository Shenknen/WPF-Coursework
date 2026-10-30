namespace EquipmentDesk.Core.Models;

public class Borrower
{
    public int Id { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string GroupOrDepartment { get; set; } = string.Empty;
    public string Contact { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
}