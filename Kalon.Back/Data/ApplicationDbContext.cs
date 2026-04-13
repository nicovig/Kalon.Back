using Kalon.Back.Models;
using Microsoft.EntityFrameworkCore;

namespace Kalon.Back.Data;

public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : DbContext(options)
{
    public DbSet<Organization> Organizations { get; set; }
    public DbSet<User> Users { get; set; }
    public DbSet<OrganizationLogo> OrganizationLogos { get; set; }
    public DbSet<Contact> Contacts { get; set; }
    public DbSet<Donation> Donations { get; set; }
    public DbSet<TaxReceipt> TaxReceipts { get; set; }
    public DbSet<EmailTemplate> EmailTemplates { get; set; }
    public DbSet<EmailLog> EmailLogs { get; set; }
    public DbSet<ContentBlock> ContentBlocks { get; set; }

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

            // relation 1-1 avec User
            entity.HasOne(o => o.User)
                .WithOne()
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
            entity.HasIndex(c => new { c.OrganizationId, c.Status });
            entity.HasIndex(c => new { c.OrganizationId, c.Kind });
            entity.HasIndex(c => new { c.OrganizationId, c.Department });
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

            entity.HasIndex(d => d.OrganizationId);
            entity.HasIndex(d => d.ContactId);
            entity.HasIndex(d => new { d.OrganizationId, d.Date });
            entity.HasIndex(d => new { d.OrganizationId, d.DonationType });
        });

        // ── TaxReceipt ────────────────────────────────────────────
        modelBuilder.Entity<TaxReceipt>(entity =>
        {
            entity.ToTable("tax_receipts");

            entity.HasOne(r => r.Organization)
                .WithMany(o => o.TaxReceipts)
                .HasForeignKey(r => r.OrganizationId)
                .OnDelete(DeleteBehavior.Restrict);

            // relation 1-1 avec Donation
            entity.HasOne(r => r.Donation)
                .WithOne(d => d.TaxReceipt)
                .HasForeignKey<TaxReceipt>(r => r.DonationId)
                .OnDelete(DeleteBehavior.Restrict);

            // deux reçus ne peuvent pas avoir le même numéro d'ordre pour la même asso
            entity.HasIndex(r => new { r.OrganizationId, r.OrderNumber }).IsUnique();
            entity.HasIndex(r => new { r.OrganizationId, r.Status });
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

        // ── EmailLog ──────────────────────────────────────────────
        modelBuilder.Entity<EmailLog>(entity =>
        {
            entity.ToTable("email_logs");

            entity.HasOne(l => l.Organization)
                .WithMany(o => o.EmailLogs)
                .HasForeignKey(l => l.OrganizationId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(l => l.Contact)
                .WithMany()
                .HasForeignKey(l => l.ContactId)
                .OnDelete(DeleteBehavior.Restrict);

            // lien optionnel vers un reçu fiscal si le mail est un envoi de Cerfa
            entity.HasOne(l => l.TaxReceipt)
                .WithMany()
                .HasForeignKey(l => l.TaxReceiptId)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasIndex(l => l.OrganizationId);
            entity.HasIndex(l => l.ContactId);
            entity.HasIndex(l => l.TaxReceiptId);
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
    }
}
