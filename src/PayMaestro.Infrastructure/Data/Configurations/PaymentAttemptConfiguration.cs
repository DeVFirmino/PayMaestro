using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PayMaestro.Domain.Entities;

namespace PayMaestro.Infrastructure.Data.Configurations;

public class PaymentAttemptConfiguration : IEntityTypeConfiguration<PaymentAttempt>
{
    public void Configure(EntityTypeBuilder<PaymentAttempt> builder)
    {
        builder.HasKey(a => a.Id);

        // The entity assigns its own id, so EF must not read a set key as "this row already
        // exists" — that turns an attempt recorded after the reservation into an UPDATE of nothing.
        builder.Property(a => a.Id).ValueGeneratedNever();
        builder.Property(a => a.GatewayName).IsRequired().HasMaxLength(50);
        builder.Property(a => a.ResultType).HasConversion<string>();
        builder.Property(a => a.ProviderIdempotencyKey).IsRequired().HasMaxLength(200);
        builder.HasIndex(a => a.CreatedAt);
    }
}
