using EquipmentDesk.Core.Models;
using Microsoft.EntityFrameworkCore;

namespace EquipmentDesk.Core.Data;

public class AppDbContext : DbContext
{
    public DbSet<Category> Categories { get; set; } = null!;
    public DbSet<EquipmentItem> EquipmentItems { get; set;} = null!;
    public DbSet<Borrower> Borrowers { get; set; } = null!;
    public DbSet<EquipmentLoan> Loans { get; set; } = null!;
    public DbSet<LoanEvent> LoanEvents { get; set; } = null!;

    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<EquipmentItem>()
            .HasIndex(e => e.InventoryNumber)
            .IsUnique();

        modelBuilder.Entity<EquipmentLoan>()
            .HasIndex(l => l.EquipmentItemId)
            .HasFilter("[ReturnedAtUtc] IS NULL")
            .IsUnique();
        
        modelBuilder.Entity<EquipmentItem>()
            .HasOne(e => e.Category)
            .WithMany()
            .HasForeignKey(e => e.CategoryId)
            .OnDelete(DeleteBehavior.Restrict);
        
        modelBuilder.Entity<EquipmentLoan>()
            .HasOne(l => l.EquipmentItem)
            .WithMany()
            .HasForeignKey(l => l.EquipmentItemId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<EquipmentLoan>()
            .HasOne(l => l.Borrower)
            .WithMany()
            .HasForeignKey(l => l.BorrowerId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}