using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using User.Domain.Entities;

namespace User.Infrastructure.Configurations;

public sealed class UserConfiguration : IEntityTypeConfiguration<UserAccount>
{
    public void Configure(EntityTypeBuilder<UserAccount> builder)
    {
        builder.ToTable("users", table =>
        {
            table.HasCheckConstraint("users_first_name_check", "length(trim(first_name)) > 0");
            table.HasCheckConstraint("users_last_name_check", "length(trim(last_name)) > 0");
            table.HasCheckConstraint("users_email_check", "email = lower(trim(email)) AND length(email) > 0");
            table.HasCheckConstraint("users_age_check", "age BETWEEN 0 AND 120");
            table.HasCheckConstraint("users_city_check", "length(trim(city)) > 0");
        });
        builder.HasKey(user => user.Id);
        builder.Property(user => user.Id).HasColumnName("id").UseIdentityAlwaysColumn();
        builder.Property(user => user.FirstName).HasColumnName("first_name").HasMaxLength(100).IsRequired();
        builder.Property(user => user.LastName).HasColumnName("last_name").HasMaxLength(100).IsRequired();
        builder.Property(user => user.Email).HasColumnName("email").HasMaxLength(254).IsRequired();
        builder.Property(user => user.Age).HasColumnName("age");
        builder.Property(user => user.City).HasColumnName("city").HasMaxLength(100).IsRequired();
        builder.Property(user => user.CreatedAt).HasColumnName("created_at")
            .HasColumnType("timestamp with time zone").HasDefaultValueSql("CURRENT_TIMESTAMP");
        builder.HasIndex(user => user.Email).IsUnique().HasDatabaseName("uq_users_email");
    }
}
