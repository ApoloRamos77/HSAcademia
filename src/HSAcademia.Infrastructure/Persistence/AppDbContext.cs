using HSAcademia.Domain.Entities;
using HSAcademia.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace HSAcademia.Infrastructure.Persistence;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    // =====================================================================
    // DbSets
    // =====================================================================
    public DbSet<Academy> Academies => Set<Academy>();
    public DbSet<User> Users => Set<User>();
    public DbSet<AcademyRegistrationRequest> RegistrationRequests => Set<AcademyRegistrationRequest>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<Headquarter> Headquarters => Set<Headquarter>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<AcademyRole> AcademyRoles => Set<AcademyRole>();
    public DbSet<Student> Students => Set<Student>();
    public DbSet<StudentMedicalRecord> StudentMedicalRecords => Set<StudentMedicalRecord>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<ProductSale> ProductSales => Set<ProductSale>();
    public DbSet<AcademyFinancialConfig> AcademyFinancialConfigs => Set<AcademyFinancialConfig>();
    public DbSet<PaymentRecord> PaymentRecords => Set<PaymentRecord>();
    public DbSet<PaymentInstallment> PaymentInstallments => Set<PaymentInstallment>();
    public DbSet<Attendance> Attendances => Set<Attendance>();
    public DbSet<Tournament> Tournaments => Set<Tournament>();
    public DbSet<Event> Events => Set<Event>();
    public DbSet<Announcement> Announcements => Set<Announcement>();

    // =====================================================================
    // Model configuration
    // =====================================================================
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // ----------------------------------------------------------------
        // Academy
        // ----------------------------------------------------------------
        modelBuilder.Entity<Academy>(entity =>
        {
            entity.ToTable("academies");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.AcademyId).HasColumnName("academy_id").IsRequired();
            entity.Property(e => e.Name).HasColumnName("name").HasMaxLength(200).IsRequired();
            entity.Property(e => e.SlugName).HasColumnName("slug_name").HasMaxLength(100).IsRequired();
            entity.Property(e => e.Description).HasColumnName("description").HasMaxLength(2000);
            entity.Property(e => e.ContactEmail).HasColumnName("contact_email").HasMaxLength(200).IsRequired();
            entity.Property(e => e.ContactPhone).HasColumnName("contact_phone").HasMaxLength(50);
            entity.Property(e => e.LogoUrl).HasColumnName("logo_url").HasMaxLength(500);
            entity.Property(e => e.Address).HasColumnName("address").HasMaxLength(500);
            entity.Property(e => e.City).HasColumnName("city").HasMaxLength(100);
            entity.Property(e => e.Country).HasColumnName("country").HasMaxLength(100);
            entity.Property(e => e.Website).HasColumnName("website").HasMaxLength(300);
            entity.Property(e => e.Sport).HasColumnName("sport").HasMaxLength(100);
            entity.Property(e => e.Status).HasColumnName("status")
                  .HasConversion<int>()
                  .HasDefaultValue(AcademyStatus.Pending);
            entity.Property(e => e.RejectionReason).HasColumnName("rejection_reason").HasMaxLength(1000);
            entity.Property(e => e.SuspensionReason).HasColumnName("suspension_reason").HasMaxLength(1000);
            entity.Property(e => e.SuspendedAt).HasColumnName("suspended_at");
            entity.Property(e => e.ApprovedAt).HasColumnName("approved_at");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("NOW()");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at").HasDefaultValueSql("NOW()");
            entity.Property(e => e.IsDeleted).HasColumnName("is_deleted").HasDefaultValue(false);
            entity.Property(e => e.DeletedAt).HasColumnName("deleted_at");

            entity.HasIndex(e => e.SlugName).IsUnique();
            entity.HasIndex(e => e.AcademyId).IsUnique();
            entity.HasIndex(e => e.Status);

            // Soft-delete global filter
            entity.HasQueryFilter(e => !e.IsDeleted);
        });

        // ----------------------------------------------------------------
        // User
        // ----------------------------------------------------------------
        modelBuilder.Entity<User>(entity =>
        {
            entity.ToTable("users");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.AcademyId).HasColumnName("academy_id");   // nullable = SuperAdmin
            entity.Property(e => e.FirstName).HasColumnName("first_name").HasMaxLength(100).IsRequired();
            entity.Property(e => e.LastName).HasColumnName("last_name").HasMaxLength(100).IsRequired();
            entity.Property(e => e.Email).HasColumnName("email").HasMaxLength(200).IsRequired();
            entity.Property(e => e.PasswordHash).HasColumnName("password_hash").HasMaxLength(500).IsRequired();
            entity.Property(e => e.Phone).HasColumnName("phone").HasMaxLength(50);
            entity.Property(e => e.AvatarUrl).HasColumnName("avatar_url").HasMaxLength(500);
            entity.Property(e => e.Role).HasColumnName("role").HasConversion<int>();
            entity.Property(e => e.Status).HasColumnName("status")
                  .HasConversion<int>()
                  .HasDefaultValue(UserStatus.Active);
            entity.Property(e => e.SuspensionReason).HasColumnName("suspension_reason").HasMaxLength(1000);
            entity.Property(e => e.SuspendedAt).HasColumnName("suspended_at");
            entity.Property(e => e.PasswordResetToken).HasColumnName("password_reset_token").HasMaxLength(500);
            entity.Property(e => e.PasswordResetExpiry).HasColumnName("password_reset_expiry");
            entity.Property(e => e.LastLoginAt).HasColumnName("last_login_at");
            entity.Property(e => e.BirthDate).HasColumnName("birth_date");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("NOW()");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at").HasDefaultValueSql("NOW()");
            entity.Property(e => e.IsDeleted).HasColumnName("is_deleted").HasDefaultValue(false);
            entity.Property(e => e.DeletedAt).HasColumnName("deleted_at");

            entity.HasIndex(e => e.Email).IsUnique();
            entity.HasIndex(e => e.AcademyId);
            entity.HasIndex(e => e.Role);

            // Soft-delete global filter
            entity.HasQueryFilter(e => !e.IsDeleted);

            // Relationship to Academy
            entity.HasOne(e => e.Academy)
                  .WithMany(a => a.Users)
                  .HasForeignKey(e => e.AcademyId)
                  .HasPrincipalKey(a => a.AcademyId)     // FK references academy_id (tenant discriminator)
                  .OnDelete(DeleteBehavior.Restrict)
                  .IsRequired(false);

            // Relationship to AcademyRole
            entity.HasOne(e => e.AcademyRole)
                  .WithMany(r => r.Users)
                  .HasForeignKey(e => e.AcademyRoleId)
                  .OnDelete(DeleteBehavior.SetNull)
                  .IsRequired(false);

            // Relationship to Headquarter
            entity.HasOne(e => e.Headquarter)
                  .WithMany(h => h.Users)
                  .HasForeignKey(e => e.HeadquarterId)
                  .OnDelete(DeleteBehavior.SetNull)
                  .IsRequired(false);
        });

        // ----------------------------------------------------------------
        // Headquarter
        // ----------------------------------------------------------------
        modelBuilder.Entity<Headquarter>(entity =>
        {
            entity.ToTable("headquarters");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.AcademyId).HasColumnName("academy_id").IsRequired();
            entity.Property(e => e.Name).HasColumnName("name").HasMaxLength(150).IsRequired();
            entity.Property(e => e.Address).HasColumnName("address").HasMaxLength(500);
            entity.Property(e => e.ContactPhone).HasColumnName("contact_phone").HasMaxLength(50);
            entity.Property(e => e.IsActive).HasColumnName("is_active").HasDefaultValue(true);
            
            entity.Property(e => e.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("NOW()");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at").HasDefaultValueSql("NOW()");
            entity.Property(e => e.IsDeleted).HasColumnName("is_deleted").HasDefaultValue(false);
            entity.Property(e => e.DeletedAt).HasColumnName("deleted_at");

            entity.HasIndex(e => e.AcademyId);
            entity.HasQueryFilter(e => !e.IsDeleted);

            entity.HasOne(e => e.Academy)
                  .WithMany(a => a.Headquarters)
                  .HasForeignKey(e => e.AcademyId)
                  .HasPrincipalKey(a => a.AcademyId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        // ----------------------------------------------------------------
        // Category
        // ----------------------------------------------------------------
        modelBuilder.Entity<Category>(entity =>
        {
            entity.ToTable("categories");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.AcademyId).HasColumnName("academy_id").IsRequired();
            entity.Property(e => e.HeadquarterId).HasColumnName("headquarter_id").IsRequired();
            entity.Property(e => e.Name).HasColumnName("name").HasMaxLength(100).IsRequired();
            entity.Property(e => e.MinAge).HasColumnName("min_age");
            entity.Property(e => e.MaxAge).HasColumnName("max_age");
            entity.Property(e => e.IsActive).HasColumnName("is_active").HasDefaultValue(true);
            
            entity.Property(e => e.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("NOW()");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at").HasDefaultValueSql("NOW()");
            entity.Property(e => e.IsDeleted).HasColumnName("is_deleted").HasDefaultValue(false);
            entity.Property(e => e.DeletedAt).HasColumnName("deleted_at");

            entity.HasIndex(e => e.AcademyId);
            entity.HasQueryFilter(e => !e.IsDeleted);

            entity.HasOne(e => e.Academy)
                  .WithMany(a => a.Categories)
                  .HasForeignKey(e => e.AcademyId)
                  .HasPrincipalKey(a => a.AcademyId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.Headquarter)
                  .WithMany(h => h.Categories)
                  .HasForeignKey(e => e.HeadquarterId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        // ----------------------------------------------------------------
        // AcademyRole
        // ----------------------------------------------------------------
        modelBuilder.Entity<AcademyRole>(entity =>
        {
            entity.ToTable("academy_roles");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.AcademyId).HasColumnName("academy_id").IsRequired();
            entity.Property(e => e.Name).HasColumnName("name").HasMaxLength(100).IsRequired();
            entity.Property(e => e.Description).HasColumnName("description").HasMaxLength(500);
            entity.Property(e => e.IsActive).HasColumnName("is_active").HasDefaultValue(true);

            entity.Property(e => e.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("NOW()");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at").HasDefaultValueSql("NOW()");
            entity.Property(e => e.IsDeleted).HasColumnName("is_deleted").HasDefaultValue(false);
            entity.Property(e => e.DeletedAt).HasColumnName("deleted_at");

            entity.HasIndex(e => e.AcademyId);
            entity.HasQueryFilter(e => !e.IsDeleted);

            entity.HasOne(e => e.Academy)
                  .WithMany(a => a.Roles)
                  .HasForeignKey(e => e.AcademyId)
                  .HasPrincipalKey(a => a.AcademyId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        // ----------------------------------------------------------------
        // AcademyRegistrationRequest
        // ----------------------------------------------------------------
        modelBuilder.Entity<AcademyRegistrationRequest>(entity =>
        {
            entity.ToTable("academy_registration_requests");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.AcademyName).HasColumnName("academy_name").HasMaxLength(200).IsRequired();
            entity.Property(e => e.Description).HasColumnName("description").HasMaxLength(2000);
            entity.Property(e => e.ContactName).HasColumnName("contact_name").HasMaxLength(200).IsRequired();
            entity.Property(e => e.ContactEmail).HasColumnName("contact_email").HasMaxLength(200).IsRequired();
            entity.Property(e => e.ContactPhone).HasColumnName("contact_phone").HasMaxLength(50);
            entity.Property(e => e.City).HasColumnName("city").HasMaxLength(100);
            entity.Property(e => e.Country).HasColumnName("country").HasMaxLength(100);
            entity.Property(e => e.Sport).HasColumnName("sport").HasMaxLength(100);
            entity.Property(e => e.Website).HasColumnName("website").HasMaxLength(300);
            entity.Property(e => e.AdditionalInfo).HasColumnName("additional_info").HasMaxLength(3000);
            entity.Property(e => e.Status).HasColumnName("status")
                  .HasConversion<int>()
                  .HasDefaultValue(RegistrationRequestStatus.Pending);
            entity.Property(e => e.ReviewNotes).HasColumnName("review_notes").HasMaxLength(2000);
            entity.Property(e => e.ReviewedByUserId).HasColumnName("reviewed_by_user_id");
            entity.Property(e => e.ReviewedAt).HasColumnName("reviewed_at");
            entity.Property(e => e.CreatedAcademyId).HasColumnName("created_academy_id");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("NOW()");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at").HasDefaultValueSql("NOW()");

            entity.HasIndex(e => e.Status);
            entity.HasIndex(e => e.ContactEmail);

            entity.HasOne(e => e.CreatedAcademy)
                  .WithMany(a => a.RegistrationRequests)
                  .HasForeignKey(e => e.CreatedAcademyId)
                  .OnDelete(DeleteBehavior.SetNull)
                  .IsRequired(false);
        });

        // ----------------------------------------------------------------
        // AuditLog
        // ----------------------------------------------------------------
        modelBuilder.Entity<AuditLog>(entity =>
        {
            entity.ToTable("audit_logs");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.AcademyId).HasColumnName("academy_id");
            entity.Property(e => e.UserId).HasColumnName("user_id");
            entity.Property(e => e.Action).HasColumnName("action").HasMaxLength(200).IsRequired();
            entity.Property(e => e.EntityType).HasColumnName("entity_type").HasMaxLength(100);
            entity.Property(e => e.EntityId).HasColumnName("entity_id").HasMaxLength(100);
            entity.Property(e => e.OldValues).HasColumnName("old_values").HasColumnType("jsonb");
            entity.Property(e => e.NewValues).HasColumnName("new_values").HasColumnType("jsonb");
            entity.Property(e => e.IpAddress).HasColumnName("ip_address").HasMaxLength(50);
            entity.Property(e => e.UserAgent).HasColumnName("user_agent").HasMaxLength(500);
            entity.Property(e => e.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("NOW()");

            entity.HasIndex(e => e.AcademyId);
            entity.HasIndex(e => e.UserId);
            entity.HasIndex(e => e.CreatedAt);

            entity.HasOne(e => e.User)
                  .WithMany(u => u.AuditLogs)
                  .HasForeignKey(e => e.UserId)
                  .OnDelete(DeleteBehavior.SetNull)
                  .IsRequired(false);
        });

        // ----------------------------------------------------------------
        // Product
        // ----------------------------------------------------------------
        modelBuilder.Entity<Product>(entity =>
        {
            entity.ToTable("products");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.AcademyId).HasColumnName("academy_id").IsRequired();
            entity.Property(e => e.Name).HasColumnName("name").HasMaxLength(150).IsRequired();
            entity.Property(e => e.Description).HasColumnName("description").HasMaxLength(500);
            entity.Property(e => e.ProductCategory).HasColumnName("product_category").HasMaxLength(100);
            entity.Property(e => e.Price).HasColumnName("price").HasColumnType("numeric(10,2)").IsRequired();
            entity.Property(e => e.Stock).HasColumnName("stock").IsRequired();
            entity.Property(e => e.IsActive).HasColumnName("is_active").HasDefaultValue(true);

            entity.Property(e => e.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("NOW()");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at").HasDefaultValueSql("NOW()");
            entity.Property(e => e.IsDeleted).HasColumnName("is_deleted").HasDefaultValue(false);
            entity.Property(e => e.DeletedAt).HasColumnName("deleted_at");

            entity.HasQueryFilter(e => !e.IsDeleted);

            entity.HasOne(e => e.Academy)
                  .WithMany()
                  .HasForeignKey(e => e.AcademyId)
                  .HasPrincipalKey(a => a.AcademyId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        // ----------------------------------------------------------------
        // ProductSale
        // ----------------------------------------------------------------
        modelBuilder.Entity<ProductSale>(entity =>
        {
            entity.ToTable("product_sales");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.AcademyId).HasColumnName("academy_id").IsRequired();
            entity.Property(e => e.ProductId).HasColumnName("product_id").IsRequired();
            entity.Property(e => e.StudentId).HasColumnName("student_id");
            entity.Property(e => e.Quantity).HasColumnName("quantity").IsRequired();
            entity.Property(e => e.UnitPrice).HasColumnName("unit_price").HasColumnType("numeric(10,2)").IsRequired();
            entity.Property(e => e.TotalPrice).HasColumnName("total_price").HasColumnType("numeric(10,2)").IsRequired();
            entity.Property(e => e.SaleDate).HasColumnName("sale_date").HasDefaultValueSql("NOW()");

            entity.Property(e => e.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("NOW()");
            entity.Property(e => e.IsDeleted).HasColumnName("is_deleted").HasDefaultValue(false);
            entity.Property(e => e.DeletedAt).HasColumnName("deleted_at");

            entity.HasQueryFilter(e => !e.IsDeleted);

            entity.HasOne(e => e.Academy)
                  .WithMany()
                  .HasForeignKey(e => e.AcademyId)
                  .HasPrincipalKey(a => a.AcademyId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.Product)
                  .WithMany()
                  .HasForeignKey(e => e.ProductId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.Student)
                  .WithMany()
                  .HasForeignKey(e => e.StudentId)
                  .OnDelete(DeleteBehavior.SetNull);
        });

        // ----------------------------------------------------------------
        // AcademyFinancialConfig
        // ----------------------------------------------------------------
        modelBuilder.Entity<AcademyFinancialConfig>(entity =>
        {
            entity.ToTable("academy_financial_configs");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.AcademyId).HasColumnName("academy_id").IsRequired();
            entity.Property(e => e.DefaultPaymentDay).HasColumnName("default_payment_day").IsRequired().HasDefaultValue(5);

            entity.Property(e => e.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("NOW()");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at").HasDefaultValueSql("NOW()");

            entity.HasOne(e => e.Academy)
                  .WithOne()
                  .HasForeignKey<AcademyFinancialConfig>(e => e.AcademyId)
                  .HasPrincipalKey<Academy>(a => a.AcademyId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        // ----------------------------------------------------------------
        // PaymentRecord
        // ----------------------------------------------------------------
        modelBuilder.Entity<PaymentRecord>(entity =>
        {
            entity.ToTable("payment_records");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.AcademyId).HasColumnName("academy_id").IsRequired();
            entity.Property(e => e.StudentId).HasColumnName("student_id").IsRequired();
            entity.Property(e => e.Description).HasColumnName("description").HasMaxLength(255).IsRequired();
            entity.Property(e => e.Amount).HasColumnName("amount").HasColumnType("numeric(10,2)").IsRequired();
            entity.Property(e => e.DueDate).HasColumnName("due_date").IsRequired();
            entity.Property(e => e.IsPaid).HasColumnName("is_paid").HasDefaultValue(false);
            entity.Property(e => e.PaidDate).HasColumnName("paid_date");
            entity.Property(e => e.Type).HasColumnName("type").HasConversion<int>().IsRequired();
            entity.Property(e => e.ProductSaleId).HasColumnName("product_sale_id");

            entity.Property(e => e.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("NOW()");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at").HasDefaultValueSql("NOW()");
            entity.Property(e => e.IsDeleted).HasColumnName("is_deleted").HasDefaultValue(false);
            entity.Property(e => e.DeletedAt).HasColumnName("deleted_at");

            entity.HasQueryFilter(e => !e.IsDeleted);

            entity.HasOne(e => e.Academy)
                  .WithMany()
                  .HasForeignKey(e => e.AcademyId)
                  .HasPrincipalKey(a => a.AcademyId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.Student)
                  .WithMany()
                  .HasForeignKey(e => e.StudentId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.ProductSale)
                  .WithMany()
                  .HasForeignKey(e => e.ProductSaleId)
                  .OnDelete(DeleteBehavior.SetNull);

            // Installments
            entity.Property(e => e.AmountPaid).HasColumnName("amount_paid").HasColumnType("numeric(10,2)").HasDefaultValue(0m);
            entity.Property(e => e.IsProrated).HasColumnName("is_prorated").HasDefaultValue(false);
            entity.Property(e => e.ProratedStartDate).HasColumnName("prorated_start_date");
            entity.Property(e => e.ProratedTotalDays).HasColumnName("prorated_total_days");
            entity.Property(e => e.ProratedDaysCharged).HasColumnName("prorated_days_charged");
            entity.Property(e => e.ExclusionType).HasColumnName("exclusion_type").HasConversion<int>().HasDefaultValue(ExclusionType.None);
            entity.Property(e => e.ExclusionNote).HasColumnName("exclusion_note").HasMaxLength(500);

            entity.HasMany(e => e.Installments)
                  .WithOne(i => i.PaymentRecord)
                  .HasForeignKey(i => i.PaymentRecordId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // ----------------------------------------------------------------
        // PaymentInstallment
        // ----------------------------------------------------------------
        modelBuilder.Entity<PaymentInstallment>(entity =>
        {
            entity.ToTable("payment_installments");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.PaymentRecordId).HasColumnName("payment_record_id").IsRequired();
            entity.Property(e => e.AmountPaid).HasColumnName("amount_paid").HasColumnType("numeric(10,2)").IsRequired();
            entity.Property(e => e.PaidAt).HasColumnName("paid_at").IsRequired();
            entity.Property(e => e.Method).HasColumnName("method").HasConversion<int>().HasDefaultValue(PaymentMethod.Cash);
            entity.Property(e => e.OperationNumber).HasColumnName("operation_number").HasMaxLength(100);
            entity.Property(e => e.VoucherUrl).HasColumnName("voucher_url").HasMaxLength(500);
            entity.Property(e => e.Notes).HasColumnName("notes").HasMaxLength(500);
            entity.Property(e => e.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("NOW()");

            entity.HasIndex(e => e.PaymentRecordId);
        });

        // ----------------------------------------------------------------
        // Attendance
        // ----------------------------------------------------------------
        modelBuilder.Entity<Attendance>(entity =>
        {
            entity.ToTable("attendances");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.AcademyId).HasColumnName("academy_id").IsRequired();
            entity.Property(e => e.StudentId).HasColumnName("student_id").IsRequired();
            entity.Property(e => e.Date).HasColumnName("date").HasColumnType("date").IsRequired();
            entity.Property(e => e.Status).HasColumnName("status").HasConversion<int>().IsRequired();
            entity.Property(e => e.Notes).HasColumnName("notes").HasMaxLength(500);
            entity.Property(e => e.EventId).HasColumnName("event_id");

            entity.Property(e => e.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("NOW()");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at").HasDefaultValueSql("NOW()");

            entity.HasIndex(e => new { e.StudentId, e.Date }).IsUnique();
            entity.HasIndex(e => e.AcademyId);
            entity.HasIndex(e => e.EventId);

            entity.HasOne(e => e.Academy)
                  .WithMany()
                  .HasForeignKey(e => e.AcademyId)
                  .HasPrincipalKey(a => a.AcademyId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.Student)
                  .WithMany()
                  .HasForeignKey(e => e.StudentId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.Event)
                  .WithMany()
                  .HasForeignKey(e => e.EventId)
                  .OnDelete(DeleteBehavior.SetNull)
                  .IsRequired(false);
        });

        // ----------------------------------------------------------------
        // Tournament
        // ----------------------------------------------------------------
        modelBuilder.Entity<Tournament>(entity =>
        {
            entity.ToTable("tournaments");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.AcademyId).HasColumnName("academy_id").IsRequired();
            entity.Property(e => e.Name).HasColumnName("name").HasMaxLength(200).IsRequired();
            entity.Property(e => e.Organizer).HasColumnName("organizer").HasMaxLength(200).IsRequired();
            entity.Property(e => e.MainLocation).HasColumnName("main_location").HasMaxLength(300).IsRequired();

            entity.Property(e => e.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("NOW()");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at").HasDefaultValueSql("NOW()");
            entity.Property(e => e.IsDeleted).HasColumnName("is_deleted").HasDefaultValue(false);
            entity.Property(e => e.DeletedAt).HasColumnName("deleted_at");

            entity.HasIndex(e => e.AcademyId);
            entity.HasQueryFilter(e => !e.IsDeleted);

            entity.HasOne(e => e.Academy)
                  .WithMany()
                  .HasForeignKey(e => e.AcademyId)
                  .HasPrincipalKey(a => a.AcademyId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        // ----------------------------------------------------------------
        // Event
        // ----------------------------------------------------------------
        modelBuilder.Entity<Event>(entity =>
        {
            entity.ToTable("events");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.AcademyId).HasColumnName("academy_id").IsRequired();
            entity.Property(e => e.Title).HasColumnName("title").HasMaxLength(200).IsRequired();
            entity.Property(e => e.Description).HasColumnName("description").HasMaxLength(1000);
            entity.Property(e => e.Type).HasColumnName("type").HasConversion<int>().IsRequired();
            entity.Property(e => e.StartTime).HasColumnName("start_time").IsRequired();
            entity.Property(e => e.EndTime).HasColumnName("end_time").IsRequired();
            entity.Property(e => e.HeadquarterId).HasColumnName("headquarter_id");
            entity.Property(e => e.CategoryId).HasColumnName("category_id");
            entity.Property(e => e.TeacherId).HasColumnName("teacher_id");
            entity.Property(e => e.TournamentId).HasColumnName("tournament_id");
            entity.Property(e => e.OpponentTeam).HasColumnName("opponent_team").HasMaxLength(200);
            entity.Property(e => e.IsActive).HasColumnName("is_active").HasDefaultValue(true);

            entity.Property(e => e.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("NOW()");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at").HasDefaultValueSql("NOW()");
            entity.Property(e => e.IsDeleted).HasColumnName("is_deleted").HasDefaultValue(false);
            entity.Property(e => e.DeletedAt).HasColumnName("deleted_at");

            entity.HasIndex(e => e.AcademyId);
            entity.HasIndex(e => e.StartTime);
            entity.HasQueryFilter(e => !e.IsDeleted);

            entity.HasOne(e => e.Academy)
                  .WithMany()
                  .HasForeignKey(e => e.AcademyId)
                  .HasPrincipalKey(a => a.AcademyId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.Headquarter)
                  .WithMany()
                  .HasForeignKey(e => e.HeadquarterId)
                  .OnDelete(DeleteBehavior.SetNull)
                  .IsRequired(false);

            entity.HasOne(e => e.Category)
                  .WithMany()
                  .HasForeignKey(e => e.CategoryId)
                  .OnDelete(DeleteBehavior.SetNull)
                  .IsRequired(false);

            entity.HasOne(e => e.Teacher)
                  .WithMany()
                  .HasForeignKey(e => e.TeacherId)
                  .OnDelete(DeleteBehavior.SetNull)
                  .IsRequired(false);

            entity.HasOne(e => e.Tournament)
                  .WithMany()
                  .HasForeignKey(e => e.TournamentId)
                  .OnDelete(DeleteBehavior.SetNull)
                  .IsRequired(false);
        });

        // ----------------------------------------------------------------
        // Announcement
        // ----------------------------------------------------------------
        modelBuilder.Entity<Announcement>(entity =>
        {
            entity.ToTable("announcements");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.AcademyId).HasColumnName("academy_id").IsRequired();
            entity.Property(e => e.Title).HasColumnName("title").HasMaxLength(200).IsRequired();
            entity.Property(e => e.Content).HasColumnName("content").HasMaxLength(3000).IsRequired();
            entity.Property(e => e.IsPinned).HasColumnName("is_pinned").HasDefaultValue(false);
            entity.Property(e => e.AuthorId).HasColumnName("author_id").IsRequired();
            entity.Property(e => e.DatePosted).HasColumnName("date_posted").HasDefaultValueSql("NOW()");

            entity.Property(e => e.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("NOW()");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at").HasDefaultValueSql("NOW()");
            entity.Property(e => e.IsDeleted).HasColumnName("is_deleted").HasDefaultValue(false);
            entity.Property(e => e.DeletedAt).HasColumnName("deleted_at");

            entity.HasIndex(e => e.AcademyId);
            entity.HasQueryFilter(e => !e.IsDeleted);

            entity.HasOne(e => e.Author)
                  .WithMany()
                  .HasForeignKey(e => e.AuthorId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        // ----------------------------------------------------------------
        // Seed: SuperAdmin user
        // ----------------------------------------------------------------
        SeedData(modelBuilder);
    }

    private static void SeedData(ModelBuilder modelBuilder)
    {
        var superAdminId = Guid.Parse("00000000-0000-0000-0000-000000000001");

        modelBuilder.Entity<User>().HasData(new User
        {
            Id = superAdminId,
            AcademyId = null,
            FirstName = "Super",
            LastName = "Admin",
            Email = "superadmin@adhsoft.com",
            PasswordHash = "$2a$11$G7Z3P7d1G3kMhY1S1iR5M.O5X9W8G2m5T4c5lR2p5n8v1M3s1x2eO", // Admin2026!
            Role = Domain.Enums.UserRole.SuperAdmin,
            Status = Domain.Enums.UserStatus.Active,
            CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            UpdatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
        });
    }

    // =====================================================================
    // Auto-update UpdatedAt on SaveChanges
    // =====================================================================
    public override int SaveChanges()
    {
        UpdateTimestamps();
        return base.SaveChanges();
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        UpdateTimestamps();
        return base.SaveChangesAsync(cancellationToken);
    }

    private void UpdateTimestamps()
    {
        var entries = ChangeTracker.Entries()
            .Where(e => e.State is EntityState.Added or EntityState.Modified);

        foreach (var entry in entries)
        {
            if (entry.Properties.Any(p => p.Metadata.Name == "UpdatedAt"))
                entry.Property("UpdatedAt").CurrentValue = DateTime.UtcNow;

            if (entry.State == EntityState.Added &&
                entry.Properties.Any(p => p.Metadata.Name == "CreatedAt"))
            {
                var created = entry.Property("CreatedAt").CurrentValue;
                if (created is DateTime dt && dt == default)
                    entry.Property("CreatedAt").CurrentValue = DateTime.UtcNow;
            }
        }
    }
}
