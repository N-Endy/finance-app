using FinanceOS.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace FinanceOS.Infrastructure.Data;

public sealed class FinanceDbContext(DbContextOptions<FinanceDbContext> options) : DbContext(options)
{
    public DbSet<Owner> Owners => Set<Owner>();
    public DbSet<FinancialAccount> Accounts => Set<FinancialAccount>();
    public DbSet<BalanceSnapshot> BalanceSnapshots => Set<BalanceSnapshot>();
    public DbSet<Envelope> Envelopes => Set<Envelope>();
    public DbSet<EnvelopeAssignment> Assignments => Set<EnvelopeAssignment>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<LedgerTransaction> Transactions => Set<LedgerTransaction>();
    public DbSet<Posting> Postings => Set<Posting>();
    public DbSet<TransactionRevision> TransactionRevisions => Set<TransactionRevision>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<ExternalNote> ExternalNotes => Set<ExternalNote>();
    public DbSet<IncomeSource> IncomeSources => Set<IncomeSource>();
    public DbSet<AllocationPlan> AllocationPlans => Set<AllocationPlan>();
    public DbSet<AllocationLine> AllocationLines => Set<AllocationLine>();
    public DbSet<FamilyRecipient> FamilyRecipients => Set<FamilyRecipient>();
    public DbSet<FamilySupport> FamilySupports => Set<FamilySupport>();
    public DbSet<Goal> Goals => Set<Goal>();
    public DbSet<Holding> Holdings => Set<Holding>();
    public DbSet<ExchangeRate> ExchangeRates => Set<ExchangeRate>();
    public DbSet<Subscription> Subscriptions => Set<Subscription>();
    public DbSet<RecurringItem> RecurringItems => Set<RecurringItem>();
    public DbSet<Business> Businesses => Set<Business>();
    public DbSet<FinancialRule> Rules => Set<FinancialRule>();
    public DbSet<RuleViolation> Violations => Set<RuleViolation>();
    public DbSet<FinancialAction> Actions => Set<FinancialAction>();
    public DbSet<PensionAccount> Pensions => Set<PensionAccount>();
    public DbSet<RetirementAssumption> RetirementAssumptions => Set<RetirementAssumption>();
    public DbSet<ActualCharge> ActualCharges => Set<ActualCharge>();

    protected override void OnModelCreating(ModelBuilder model)
    {
        model.Entity<Owner>().HasIndex(x => x.Email).IsUnique();
        model.Entity<FinancialAccount>().HasIndex(x => new { x.OwnerId, x.Slug }).IsUnique();
        model.Entity<Envelope>().HasIndex(x => new { x.OwnerId, x.Slug }).IsUnique();
        model.Entity<Category>().HasIndex(x => new { x.OwnerId, x.Slug }).IsUnique();
        model.Entity<Goal>().HasIndex(x => new { x.OwnerId, x.Slug }).IsUnique();
        model.Entity<Holding>().HasIndex(x => new { x.OwnerId, x.Slug }).IsUnique();
        model.Entity<IncomeSource>().HasIndex(x => new { x.OwnerId, x.Slug }).IsUnique();
        model.Entity<Business>().HasIndex(x => new { x.OwnerId, x.Slug }).IsUnique();
        model.Entity<FinancialRule>().HasIndex(x => new { x.OwnerId, x.Code }).IsUnique();
        model.Entity<ExchangeRate>().Property(x => x.Rate).HasPrecision(18, 6);
        model.Entity<RetirementAssumption>().Property(x => x.ConservativeReturn).HasPrecision(8, 4);
        model.Entity<RetirementAssumption>().Property(x => x.BaseReturn).HasPrecision(8, 4);
        model.Entity<RetirementAssumption>().Property(x => x.AggressiveReturn).HasPrecision(8, 4);
        model.Entity<RetirementAssumption>().Property(x => x.Inflation).HasPrecision(8, 4);
        model.Entity<RetirementAssumption>().Property(x => x.WithdrawalRate).HasPrecision(8, 4);
        model.Entity<PensionAccount>().Property(x => x.AssumedReturn).HasPrecision(8, 4);
        model.Entity<LedgerTransaction>()
            .HasMany(x => x.Postings)
            .WithOne()
            .HasForeignKey(x => x.TransactionId);
        model.Entity<LedgerTransaction>()
            .HasMany(x => x.Revisions)
            .WithOne()
            .HasForeignKey(x => x.TransactionId);
        model.Entity<AllocationPlan>()
            .HasMany(x => x.Lines)
            .WithOne()
            .HasForeignKey(x => x.AllocationPlanId);
        model.Entity<FinancialAccount>()
            .HasMany(x => x.Snapshots)
            .WithOne()
            .HasForeignKey(x => x.AccountId);
        model.Entity<FinancialAccount>()
            .HasMany(x => x.Assignments)
            .WithOne()
            .HasForeignKey(x => x.AccountId);
    }
}
