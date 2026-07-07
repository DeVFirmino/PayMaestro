using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PayMaestro.Domain.Entities;

namespace PayMaestro.Infrastructure.Data.Configurations;

public class PaymentConfiguration : IEntityTypeConfiguration<Payment>
{
    public void Configure(EntityTypeBuilder<Payment> builder)
    {
        builder.HasKey(p => p.Id);

        builder.HasIndex(p => p.IdempotencyKey).IsUnique();    
        builder.Property(p => p.IdempotencyKey).IsRequired().HasMaxLength(100);

        builder.Property(p => p.Amount).HasPrecision(18, 2);  //  
        builder.Property(p => p.Currency).IsRequired().HasMaxLength(3);
        builder.Property(p => p.CardBin).HasMaxLength(6);
        builder.Property(p => p.CardLast4).HasMaxLength(4);
        builder.Property(p => p.Status).HasConversion<string>();

        builder.HasMany(p => p.Attempts).WithOne().HasForeignKey(a => a.PaymentId);
        builder.HasMany(p => p.FraudFlags).WithOne().HasForeignKey(f => f.PaymentId);
        
        
    }
}