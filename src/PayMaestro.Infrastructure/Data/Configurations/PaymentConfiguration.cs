using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PayMaestro.Domain.Entities;

namespace PayMaestro.Infrastructure.Data.Configurations;

public class PaymentConfiguration : IEntityTypeConfiguration<Payment>
{
    public void Configure(EntityTypeBuilder<Payment> builder)
    {
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).ValueGeneratedNever();

        builder.HasIndex(p => p.IdempotencyKey).IsUnique();    
        builder.Property(p => p.IdempotencyKey).IsRequired().HasMaxLength(100);

        builder.Property(p => p.Amount).HasPrecision(18, 2);  //  
        builder.Property(p => p.Currency).IsRequired().HasMaxLength(3);
        builder.Property(p => p.CardBin).HasMaxLength(6);
        builder.Property(p => p.CardLast4).HasMaxLength(4);
        builder.Property(p => p.Status).HasConversion<string>();

        // Optimistic concurrency: the UPDATE carries the stamp the writer loaded, so a writer
        // working from a stale payment fails instead of overwriting a settled outcome.
        builder.Property(p => p.ConcurrencyStamp).IsConcurrencyToken();

        // Payment.Create guards this too, but a C# factory only holds while every
        // write goes through it. A migration or a bulk import would not.
        // The CAST is load-bearing: SQLite stores decimal as TEXT, so a bare
        // "Amount > 0" compares strings and lets '0.00' through.
        builder.ToTable(t => t.HasCheckConstraint(
            "CK_Payment_Amount_Positive", "CAST(Amount AS REAL) > 0"));

        // Restrict: attempts and fraud flags are audit evidence — deleting a
        // payment must never silently delete its history.
        builder.HasMany(p => p.Attempts).WithOne()
            .HasForeignKey(a => a.PaymentId).OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(p => p.FraudFlags).WithOne()
            .HasForeignKey(f => f.PaymentId).OnDelete(DeleteBehavior.Restrict);
    }
}