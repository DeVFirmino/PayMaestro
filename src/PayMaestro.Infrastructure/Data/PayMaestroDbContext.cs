using Microsoft.EntityFrameworkCore;
using PayMaestro.Domain.Entities;

namespace PayMaestro.Infrastructure.Data;



public class PayMaestroDbContext(DbContextOptions<PayMaestroDbContext> options) : DbContext(options)
{
    public DbSet<Payment> Payment => Set<Payment>();
    public DbSet<PaymentAttempt> PaymentAttempt => Set<PaymentAttempt>();
    public DbSet<FraudFlag> FraudFlags => Set<FraudFlag>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
        => modelBuilder.ApplyConfigurationsFromAssembly(typeof(PayMaestroDbContext).Assembly);

}