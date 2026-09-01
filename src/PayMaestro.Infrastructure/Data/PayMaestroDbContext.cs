using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PayMaestro.Domain.Entities;

namespace PayMaestro.Infrastructure.Data;

public sealed class PayMaestroDbContext : DbContext
{
    public PayMaestroDbContext(DbContextOptions<PayMaestroDbContext> options)
        : base(options)
    {
    }

    public DbSet<Payment> Payments => Set<Payment>();

    public DbSet<PaymentAttempt> PaymentAttempts => Set<PaymentAttempt>();

    public DbSet<FraudFlag> FraudFlags => Set<FraudFlag>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ConfigurePayment(modelBuilder.Entity<Payment>());
        ConfigurePaymentAttempt(modelBuilder.Entity<PaymentAttempt>());
        ConfigureFraudFlag(modelBuilder.Entity<FraudFlag>());
    }

    private static void ConfigurePayment(EntityTypeBuilder<Payment> payment)
    {
        // Payment.Create guards the amount too, but a C# factory only holds while every
        // write goes through it. A migration or a bulk import would not.
        // The CAST is load-bearing: SQLite stores decimal as TEXT, so a bare
        // "Amount > 0" compares strings and lets '0.00' through.
        payment.ToTable("Payment", table => table.HasCheckConstraint(
            "CK_Payment_Amount_Positive", "CAST(Amount AS REAL) > 0"));

        payment.HasKey(entity => entity.Id);
        payment.Property(entity => entity.Id).ValueGeneratedNever();

        payment.HasIndex(entity => entity.IdempotencyKey).IsUnique();
        payment.Property(entity => entity.IdempotencyKey).IsRequired().HasMaxLength(100);

        payment.Property(entity => entity.Amount).HasPrecision(18, 2);
        payment.Property(entity => entity.Currency).IsRequired().HasMaxLength(3);
        payment.Property(entity => entity.CardBin).HasMaxLength(Payment.CardBinLength);
        payment.Property(entity => entity.CardLast4).HasMaxLength(Payment.CardLast4Length);
        payment.Property(entity => entity.Status).HasConversion<string>();

        // Optimistic concurrency: the UPDATE carries the stamp the writer loaded, so a writer
        // working from a stale payment fails instead of overwriting a settled outcome.
        payment.Property(entity => entity.ConcurrencyStamp).IsConcurrencyToken();

        // Restrict: attempts and fraud flags are audit evidence — deleting a
        // payment must never silently delete its history.
        payment.HasMany(entity => entity.Attempts).WithOne()
            .HasForeignKey(attempt => attempt.PaymentId).OnDelete(DeleteBehavior.Restrict);
        payment.HasMany(entity => entity.FraudFlags).WithOne()
            .HasForeignKey(flag => flag.PaymentId).OnDelete(DeleteBehavior.Restrict);
    }

    private static void ConfigurePaymentAttempt(EntityTypeBuilder<PaymentAttempt> attempt)
    {
        attempt.ToTable("PaymentAttempt");

        attempt.HasKey(entity => entity.Id);

        // The entity assigns its own id, so EF must not read a set key as "this row already
        // exists" — that turns an attempt recorded after the reservation into an UPDATE of nothing.
        attempt.Property(entity => entity.Id).ValueGeneratedNever();
        attempt.Property(entity => entity.GatewayName).IsRequired().HasMaxLength(50);
        attempt.Property(entity => entity.ResultType).HasConversion<string>();
        attempt.Property(entity => entity.ProviderIdempotencyKey).IsRequired().HasMaxLength(200);
        attempt.HasIndex(entity => entity.CreatedAt);
    }

    private static void ConfigureFraudFlag(EntityTypeBuilder<FraudFlag> flag)
    {
        flag.ToTable("FraudFlags");

        flag.HasKey(entity => entity.Id);
        flag.Property(entity => entity.Id).ValueGeneratedNever();
        flag.Property(entity => entity.RuleName).IsRequired().HasMaxLength(50);
        flag.Property(entity => entity.Details).IsRequired().HasMaxLength(500);
    }
}
