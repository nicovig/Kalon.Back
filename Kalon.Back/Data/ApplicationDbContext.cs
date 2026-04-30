using System.Text.Json;
using Kalon.Back.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Kalon.Back.Data;

public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : DbContext(options)
{
    public DbSet<Organization> Organizations { get; set; }
    public DbSet<User> Users { get; set; }
    public DbSet<OrganizationLogo> OrganizationLogos { get; set; }
    public DbSet<Contact> Contacts { get; set; }
    public DbSet<Donation> Donations { get; set; }
    public DbSet<GeneratedDocument> GeneratedDocuments { get; set; }
    public DbSet<EmailTemplate> EmailTemplates { get; set; }
    public DbSet<MailLog> MailLogs { get; set; }
    public DbSet<ContentBlock> ContentBlocks { get; set; }
    public DbSet<ContactStatusSettings> ContactStatusSettings { get; set; }
    public DbSet<QuotaUsage> QuotaUsages { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // ── User ──────────────────────────────────────────────────
        modelBuilder.Entity<User>(entity =>
        {
            entity.ToTable("users");
            entity.HasIndex(u => u.MeranId).IsUnique();
            entity.HasIndex(u => u.Email).IsUnique();
        });

        // ── Organization ──────────────────────────────────────────
        modelBuilder.Entity<Organization>(entity =>
        {
            entity.ToTable("organizations");

            var sendingPrefsConverter = new ValueConverter<List<string>, string>(
                v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => DeserializeSendingPreferences(v));
            entity.Property(o => o.SendingPreferences)
                .HasConversion(sendingPrefsConverter)
                .Metadata.SetValueComparer(new ValueComparer<List<string>>(
                    (a, b) => (a == null && b == null) || (a != null && b != null && a.SequenceEqual(b)),
                    c => c.Aggregate(17, (acc, x) => HashCode.Combine(acc, x.GetHashCode(StringComparison.Ordinal))),
                    c => c.ToList()));

            // relation 1-1 avec User
            entity.HasOne(o => o.User)
                .WithOne(u => u.Organization)
                .HasForeignKey<Organization>(o => o.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(o => o.UserId).IsUnique();
        });

        // ── OrganizationLogo ──────────────────────────────────────
        modelBuilder.Entity<OrganizationLogo>(entity =>
        {
            entity.ToTable("organization_logos");

            entity.HasOne(l => l.Organization)
                .WithOne(o => o.Logo)
                .HasForeignKey<OrganizationLogo>(l => l.OrganizationId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(l => l.OrganizationId).IsUnique();
        });

        // ── Contact ───────────────────────────────────────────────
        modelBuilder.Entity<Contact>(entity =>
        {
            entity.ToTable("contacts");

            entity.HasOne(c => c.Organization)
                .WithMany(o => o.Contacts)
                .HasForeignKey(c => c.OrganizationId)
                .OnDelete(DeleteBehavior.Restrict);

            // owned entities — stockées dans la table contacts
            entity.OwnsOne(c => c.Address, address =>
            {
                address.Property(a => a.Street).HasColumnName("address_street");
                address.Property(a => a.PostalCode).HasColumnName("address_postal_code");
                address.Property(a => a.City).HasColumnName("address_city");
                address.Property(a => a.Country).HasColumnName("address_country");
                address.Property(a => a.Phone).HasColumnName("address_phone");
                address.Property(a => a.Email).HasColumnName("address_email");
            });

            entity.OwnsOne(c => c.Enterprise, enterprise =>
            {
                enterprise.Property(e => e.Name).HasColumnName("enterprise_name");
                enterprise.Property(e => e.Siret).HasColumnName("enterprise_siret");
                enterprise.Property(e => e.SupportKind).HasColumnName("enterprise_support_kind");
                enterprise.Property(e => e.Street).HasColumnName("enterprise_street");
                enterprise.Property(e => e.PostalCode).HasColumnName("enterprise_postal_code");
                enterprise.Property(e => e.City).HasColumnName("enterprise_city");
                enterprise.Property(e => e.Country).HasColumnName("enterprise_country");
                enterprise.Property(e => e.ContactFirstname).HasColumnName("enterprise_contact_firstname");
                enterprise.Property(e => e.ContactLastname).HasColumnName("enterprise_contact_lastname");
                enterprise.Property(e => e.ContactEmail).HasColumnName("enterprise_contact_email");
                enterprise.Property(e => e.ContactPhone).HasColumnName("enterprise_contact_phone");
            });

            entity.HasIndex(c => c.OrganizationId);
            entity.HasIndex(c => new { c.OrganizationId, c.Kind });
            entity.HasIndex(c => new { c.OrganizationId, c.Department });
        });

        modelBuilder.Entity<ContactStatusSettings>(entity =>
        {
            entity.ToTable("contact_status_settings");

            entity.HasOne(s => s.Organization)
                .WithOne(o => o.ContactStatusSettings)
                .HasForeignKey<ContactStatusSettings>(s => s.OrganizationId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(s => s.OrganizationId).IsUnique();
        });

        // ── Donation ──────────────────────────────────────────────
        modelBuilder.Entity<Donation>(entity =>
        {
            entity.ToTable("donations");

            entity.HasOne(d => d.Organization)
                .WithMany()
                .HasForeignKey(d => d.OrganizationId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(d => d.Contact)
                .WithMany(c => c.Donations)
                .HasForeignKey(d => d.ContactId)
                .OnDelete(DeleteBehavior.Restrict);

            // plusieurs donations peuvent être liées au même document généré
            // ex: reçu fiscal annuel récapitulatif
            entity.HasOne(d => d.GeneratedDocument)
                .WithMany(doc => doc.Donations)
                .HasForeignKey(d => d.GeneratedDocumentId)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasIndex(d => d.OrganizationId);
            entity.HasIndex(d => d.ContactId);
            entity.HasIndex(d => d.GeneratedDocumentId);
            entity.HasIndex(d => new { d.OrganizationId, d.Date });
            entity.HasIndex(d => new { d.OrganizationId, d.DonationType });
        });

        // ── GeneratedDocument ────────────────────────────────────────────
        modelBuilder.Entity<GeneratedDocument>(entity =>
        {
            entity.ToTable("generated_documents");

            entity.HasOne(r => r.Organization)
                .WithMany(o => o.GeneratedDocuments)
                .HasForeignKey(r => r.OrganizationId)
                .OnDelete(DeleteBehavior.Restrict);

            // OrderNumber peut être null (membership_certificate) — index partiel pas supporté
            // nativement par EF Core, on garde juste l'index non-unique
            entity.HasIndex(r => new { r.OrganizationId, r.OrderNumber });
            entity.HasIndex(r => new { r.OrganizationId, r.Status });
            entity.HasIndex(r => new { r.OrganizationId, r.DocumentType });
        });

        // ── EmailTemplate ─────────────────────────────────────────
        modelBuilder.Entity<EmailTemplate>(entity =>
        {
            entity.ToTable("email_templates");

            entity.HasOne(t => t.Organization)
                .WithMany(o => o.EmailTemplates)
                .HasForeignKey(t => t.OrganizationId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(t => t.OrganizationId);
        });

        // ── MailLog ──────────────────────────────────────────────
        modelBuilder.Entity<MailLog>(entity =>
        {
            entity.ToTable("mail_logs");

            entity.HasOne(l => l.Organization)
                .WithMany(o => o.MailLogs)
                .HasForeignKey(l => l.OrganizationId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(l => l.Contact)
                .WithMany()
                .HasForeignKey(l => l.ContactId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(l => l.GeneratedDocument)
                .WithMany()
                .HasForeignKey(l => l.GeneratedDocumentId)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasIndex(l => l.OrganizationId);
            entity.HasIndex(l => l.ContactId);
            entity.HasIndex(l => l.GeneratedDocumentId);
        });

        // ── ContentBlock ──────────────────────────────────────────
        modelBuilder.Entity<ContentBlock>(entity =>
        {
            entity.ToTable("content_blocks");

            entity.HasOne(cb => cb.Organization)
                .WithMany(o => o.ContentBlocks)
                .HasForeignKey(cb => cb.OrganizationId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(cb => new { cb.OrganizationId, cb.Kind });
        });

        // ── QuotaUsages ──────────────────────────────────────────
        modelBuilder.Entity<QuotaUsage>(entity =>
        {
            entity.ToTable("quota_usages");

            entity.HasOne(q => q.Organization)
                .WithMany()
                .HasForeignKey(q => q.OrganizationId)
                .OnDelete(DeleteBehavior.Cascade);

            // index unique — une seule ligne par org/type/période
            entity.HasIndex(q => new { q.OrganizationId, q.QuotaType, q.Period })
                .IsUnique();

            entity.HasIndex(q => q.OrganizationId);
        });
    }

    private static List<string> DeserializeSendingPreferences(string? v)
    {
        if (string.IsNullOrWhiteSpace(v))
            return [];
        try
        {
            var list = JsonSerializer.Deserialize<List<string>>(v);
            if (list is not null)
                return list;
        }
        catch (JsonException)
        {
        }

        return [v.Trim()];
    }
}
