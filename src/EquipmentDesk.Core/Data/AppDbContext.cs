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

        //категории
        modelBuilder.Entity<Category>(entity =>
        {
            entity.Property(c => c.Name)
                .IsRequired()
                .HasMaxLength(100);

            entity.Property(c => c.NormalizedName)
                .IsRequired()
                .HasMaxLength(100);

                entity.HasIndex(c => c.NormalizedName)
                    .IsUnique();

             entity.HasMany(c => c.Items)
                .WithOne(e => e.Category)
                .HasForeignKey(e => e.CategoryId)
                .OnDelete(DeleteBehavior.Restrict);      
        });
        //уникальный инвентарный номер
        modelBuilder.Entity<EquipmentItem>()
                    .HasIndex(e => e.InventoryNumber)
                    .IsUnique();
        
        modelBuilder.Entity<EquipmentLoan>(entity =>
        {
            // не более одной открытой выдачи на экземпляр
            entity.HasIndex(l => l.EquipmentItemId)
                .HasFilter("[ReturnedAtUtc] IS NULL")
                .IsUnique();

            entity.HasOne(l => l.EquipmentItem)
                .WithMany()
                .HasForeignKey(l => l.EquipmentItemId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(l => l.Borrower)
                .WithMany()
                .HasForeignKey(l => l.BorrowerId)
                .OnDelete(DeleteBehavior.Restrict); 
        });

        modelBuilder.Entity<EquipmentItem>()
            .Property(e => e.ServiceState)
            .HasConversion<string>()
            .HasMaxLength(20);

        modelBuilder.Entity<LoanEvent>()
            .Property(e => e.EventType)
            .HasConversion<string>()
            .HasMaxLength(20);    
    }
}