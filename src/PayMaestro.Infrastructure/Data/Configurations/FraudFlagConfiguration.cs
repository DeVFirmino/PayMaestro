using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PayMaestro.Domain.Entities;

namespace PayMaestro.Infrastructure.Data.Configurations;

public class FraudFlagConfiguration : IEntityTypeConfiguration<FraudFlag>
{
    public void Configure(EntityTypeBuilder<FraudFlag> builder)
    {
        builder.HasKey(f => f.Id);
        builder.Property(f => f.Id).ValueGeneratedNever();
        builder.Property(f => f.RuleName).IsRequired().HasMaxLength(50);
        builder.Property(f => f.Details).IsRequired().HasMaxLength(500);
    }

}
