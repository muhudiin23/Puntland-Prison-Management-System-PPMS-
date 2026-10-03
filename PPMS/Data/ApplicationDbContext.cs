using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using PPMS.Models;

namespace PPMS.Data
{
    public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) { }

        public DbSet<Prison> Prisons { get; set; }
        public DbSet<Prisoner> Prisoners { get; set; }
        public DbSet<Staff> Staff { get; set; }
        public DbSet<WantedCriminal> WantedCriminals { get; set; }
        public DbSet<Alert> Alerts { get; set; }
        public DbSet<Activity> Activities { get; set; }
        public DbSet<CaseReport> CaseReports { get; set; }
        public DbSet<StaffCertificate> StaffCertificates { get; set; }
        public DbSet<PrisonerEvidence> PrisonerEvidences { get; set; }
        public DbSet<FormerPrisoner> FormerPrisoners { get; set; }
        public DbSet<PrisonerTransfer> PrisonerTransfers { get; set; }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            builder.Entity<ApplicationUser>(e =>
            {
                e.HasOne(u => u.AssignedPrison)
                 .WithMany()
                 .HasForeignKey(u => u.AssignedPrisonId)
                 .OnDelete(DeleteBehavior.SetNull);
            });

            builder.Entity<Prison>(e =>
            {
                e.HasIndex(p => p.PrisonName).IsUnique();
                e.Property(p => p.SecurityLevel).HasDefaultValue("Medium");
            });

            builder.Entity<Prisoner>(e =>
            {
                e.HasIndex(p => p.PrisonerId).IsUnique();
                e.HasIndex(p => p.NationalId);
                e.HasIndex(p => p.IsArchived);
                e.Property(p => p.IsArchived).HasDefaultValue(false);
                e.HasOne(p => p.Prison)
                 .WithMany(pr => pr.Prisoners)
                 .HasForeignKey(p => p.PrisonId)
                 .OnDelete(DeleteBehavior.Restrict);
            });

            builder.Entity<Staff>(e =>
            {
                e.HasIndex(s => s.StaffIdNumber).IsUnique();
                e.HasOne(s => s.Prison)
                 .WithMany(p => p.Staff)
                 .HasForeignKey(s => s.PrisonId)
                 .OnDelete(DeleteBehavior.Restrict);
            });

            builder.Entity<StaffCertificate>(e =>
            {
                e.HasOne(c => c.Staff)
                 .WithMany(s => s.Certificates)
                 .HasForeignKey(c => c.StaffId)
                 .OnDelete(DeleteBehavior.Cascade);
            });

            builder.Entity<PrisonerEvidence>(e =>
            {
                e.HasOne(ev => ev.Prisoner)
                 .WithMany(p => p.Evidences)
                 .HasForeignKey(ev => ev.PrisonerId)
                 .OnDelete(DeleteBehavior.Cascade);
            });

            builder.Entity<WantedCriminal>(e =>
            {
                e.HasIndex(w => w.NationalId);
            });

            builder.Entity<Alert>(e =>
            {
                e.HasIndex(a => a.IsActive);
                e.HasOne<Prison>()
                 .WithMany()
                 .HasForeignKey(a => a.PrisonId)
                 .OnDelete(DeleteBehavior.SetNull);
            });

            builder.Entity<PrisonerTransfer>(e =>
            {
                e.HasOne(t => t.Prisoner)
                 .WithMany()
                 .HasForeignKey(t => t.PrisonerId)
                 .OnDelete(DeleteBehavior.Restrict);
                e.HasOne(t => t.SourcePrison)
                 .WithMany()
                 .HasForeignKey(t => t.SourcePrisonId)
                 .OnDelete(DeleteBehavior.Restrict);
                e.HasOne(t => t.DestinationPrison)
                 .WithMany()
                 .HasForeignKey(t => t.DestinationPrisonId)
                 .OnDelete(DeleteBehavior.Restrict);
            });

            builder.Entity<CaseReport>(e =>
            {
                e.HasIndex(c => c.CaseId).IsUnique();
                e.HasIndex(c => c.CrimeType);
                e.HasIndex(c => c.CaseStatus);
                e.HasIndex(c => c.DateOfCrime);
                e.HasOne(c => c.Prison)
                 .WithMany()
                 .HasForeignKey(c => c.PrisonId)
                 .OnDelete(DeleteBehavior.SetNull);
            });
        }
    }
}
