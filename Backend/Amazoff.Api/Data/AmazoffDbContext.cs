using Amazoff.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Amazoff.Api.Data;

public sealed class AmazoffDbContext(DbContextOptions<AmazoffDbContext> options) : DbContext(options)
{
    public DbSet<Category> Categories => Set<Category>();

    public DbSet<Role> Roles => Set<Role>();

    public DbSet<User> Users => Set<User>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Role>(entity =>
        {
            entity.ToTable("roles");

            entity.HasKey(role => role.Id);

            entity.Property(role => role.Id)
                .HasColumnName("id")
                .ValueGeneratedOnAdd();

            entity.Property(role => role.Name)
                .HasColumnName("role")
                .HasMaxLength(80)
                .HasDefaultValue("user");
        });

        modelBuilder.Entity<Category>(entity =>
        {
            entity.ToTable("categorias");

            entity.HasKey(category => category.Id);

            entity.Property(category => category.Id)
                .HasColumnName("id")
                .ValueGeneratedOnAdd();

            entity.Property(category => category.Name)
                .HasColumnName("nome")
                .HasMaxLength(255);

            entity.Property(category => category.IsActive)
                .HasColumnName("is_active")
                .HasDefaultValue(true);

            entity.Property(category => category.DateCreated)
                .HasColumnName("date_created")
                .HasColumnType("datetime")
                .HasDefaultValueSql("CURRENT_TIMESTAMP");

            entity.Property(category => category.DateChanged)
                .HasColumnName("date_changed")
                .HasColumnType("datetime")
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .ValueGeneratedOnAddOrUpdate();
        });

        modelBuilder.Entity<User>(entity =>
        {
            entity.ToTable("users");

            entity.HasKey(user => user.Id);

            entity.Property(user => user.Id)
                .HasColumnName("id")
                .ValueGeneratedOnAdd();

            entity.Property(user => user.Username)
                .HasColumnName("username")
                .HasMaxLength(80);

            entity.Property(user => user.Email)
                .HasColumnName("email")
                .HasMaxLength(255);

            entity.Property(user => user.PasswordHash)
                .HasColumnName("password_hash")
                .HasMaxLength(255);

            entity.Property(user => user.FirstName)
                .HasColumnName("first_name")
                .HasMaxLength(120);

            entity.Property(user => user.LastName)
                .HasColumnName("last_name")
                .HasMaxLength(120);

            entity.Property(user => user.ImagePath)
                .HasColumnName("image_path")
                .HasMaxLength(500);

            entity.Property(user => user.IsActive)
                .HasColumnName("is_active")
                .HasDefaultValue(true);

            entity.Property(user => user.RoleId)
                .HasColumnName("id_role");

            entity.Property(user => user.LastLoginAt)
                .HasColumnName("last_login_at");

            entity.Property(user => user.CreatedAt)
                .HasColumnName("created_at")
                .HasDefaultValueSql("CURRENT_TIMESTAMP");

            entity.Property(user => user.UpdatedAt)
                .HasColumnName("updated_at")
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .ValueGeneratedOnAddOrUpdate();

            entity.HasIndex(user => user.Username)
                .IsUnique()
                .HasDatabaseName("uk_users_username");

            entity.HasIndex(user => user.Email)
                .IsUnique()
                .HasDatabaseName("uk_users_email");

            entity.HasIndex(user => user.IsActive)
                .HasDatabaseName("ix_users_is_active");

            entity.HasOne(user => user.Role)
                .WithMany(role => role.Users)
                .HasForeignKey(user => user.RoleId)
                .OnDelete(DeleteBehavior.SetNull);
        });
    }
}
