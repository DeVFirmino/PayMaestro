using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PayMaestro.Domain.Entities;

namespace PayMaestro.Infrastructure.Data.Configurations;

public class PaymentAttemptConfiguration : IEntityTypeConfiguration<PaymentAttempt>
{
    public void Configure(EntityTypeBuilder<PaymentAttempt> builder)
    {
        builder.HasKey(a => a.Id);
        builder.Property(a => a.GatewayName).IsRequired().HasMaxLength(50);
        builder.Property(a => a.ResultType).HasConversion<string>();
        builder.HasIndex(a => a.CreatedAt);   
    }
}
