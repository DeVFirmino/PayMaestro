using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using PayMaestro.Domain.Entities;

namespace PayMaestro.Infrastructure.Data;

public sealed class PayMaestroDbContext : DbContext
{
    public PayMaestroDbContext(DbContextOptions<PayMaestroDbContext> options)
        : base(options)
    {
    }

    public DbSet<Payment> Payment => Set<Payment>();
    public DbSet<PaymentAttempt> PaymentAttempt => Set<PaymentAttempt>();
    public DbSet<FraudFlag> FraudFlags => Set<FraudFlag>();

    // Timestamps are written with DateTime.UtcNow, but SQLite hands them back with an
    // Unspecified kind. Restamping them as UTC on the way out keeps a reloaded payment
    // identical to the one the original request answered with — serialized without the
    // UTC marker, the "same" timestamp reads as a different instant to the client.
    private static readonly ValueConverter<DateTime, DateTime> UtcTimestamp = new(
        value => value,
        value => DateTime.SpecifyKind(value, DateTimeKind.Utc));

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ConfigurePayment(modelBuilder);
        ConfigurePaymentAttempt(modelBuilder);
        ConfigureFraudFlag(modelBuilder);
    }

    private static void ConfigurePayment(ModelBuilder modelBuilder)
    {
        EntityTypeBuilder<Payment> payment = modelBuilder.Entity<Payment>();

        payment.HasKey(entity => entity.Id);
        payment.Property(entity => entity.Id).ValueGeneratedNever();

        payment.HasIndex(entity => new { entity.MerchantId, entity.IdempotencyKey }).IsUnique();
        payment.Property(entity => entity.MerchantId).IsRequired().HasMaxLength(100);
        payment.Property(entity => entity.IdempotencyKey).IsRequired().HasMaxLength(100);
        payment.Property(entity => entity.RequestFingerprint).IsRequired().HasMaxLength(128);

        payment.Property(entity => entity.Amount).HasPrecision(18, 2);
        payment.Property(entity => entity.Currency).IsRequired().HasMaxLength(3);
        payment.Property(entity => entity.CardBin).HasMaxLength(6);
        payment.Property(entity => entity.CardLast4).HasMaxLength(4);
        payment.Property(entity => entity.PaymentMethodToken).IsRequired().HasMaxLength(128);
        payment.Property(entity => entity.Status).HasConversion<string>();
        payment.Property(entity => entity.CreatedAt).HasConversion(UtcTimestamp);
        payment.Property(entity => entity.UpdatedAt).HasConversion(UtcTimestamp);

        // Optimistic concurrency: the UPDATE carries the stamp the writer loaded, so a writer
        // working from a stale payment fails instead of overwriting a settled outcome.
        payment.Property(entity => entity.ConcurrencyStamp).IsConcurrencyToken();

        // Payment.Create guards this too, but a C# factory only holds while every
        // write goes through it. A migration or a bulk import would not.
        // The CAST is load-bearing: SQLite stores decimal as TEXT, so a bare
        // "Amount > 0" compares strings and lets '0.00' through.
        payment.ToTable(table => table.HasCheckConstraint(
            "CK_Payment_Amount_Positive", "CAST(Amount AS REAL) > 0"));

        // Restrict: attempts and fraud flags are audit evidence — deleting a
        // payment must never silently delete its history.
        payment.HasMany(entity => entity.Attempts).WithOne()
            .HasForeignKey(attempt => attempt.PaymentId).OnDelete(DeleteBehavior.Restrict);
        payment.HasMany(entity => entity.FraudFlags).WithOne()
            .HasForeignKey(flag => flag.PaymentId).OnDelete(DeleteBehavior.Restrict);
    }

    private static void ConfigurePaymentAttempt(ModelBuilder modelBuilder)
    {
        EntityTypeBuilder<PaymentAttempt> attempt = modelBuilder.Entity<PaymentAttempt>();

        attempt.HasKey(entity => entity.Id);

        // The entity assigns its own id, so EF must not read a set key as "this row already
        // exists" — that turns an attempt recorded after the reservation into an UPDATE of nothing.
        attempt.Property(entity => entity.Id).ValueGeneratedNever();
        attempt.Property(entity => entity.GatewayName).IsRequired().HasMaxLength(50);
        attempt.Property(entity => entity.Status).HasConversion<string>();
        attempt.Property(entity => entity.ResultType).HasConversion<string>();
        attempt.Property(entity => entity.ProviderIdempotencyKey).IsRequired().HasMaxLength(200);
        attempt.Property(entity => entity.CreatedAt).HasConversion(UtcTimestamp);
        attempt.HasIndex(entity => entity.CreatedAt);
    }

    private static void ConfigureFraudFlag(ModelBuilder modelBuilder)
    {
        EntityTypeBuilder<FraudFlag> flag = modelBuilder.Entity<FraudFlag>();

        flag.HasKey(entity => entity.Id);
        flag.Property(entity => entity.Id).ValueGeneratedNever();
        flag.Property(entity => entity.RuleName).IsRequired().HasMaxLength(50);
        flag.Property(entity => entity.Details).IsRequired().HasMaxLength(500);
        flag.Property(entity => entity.CreatedAt).HasConversion(UtcTimestamp);
    }
}
