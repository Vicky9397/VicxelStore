using Dpm.BuildingBlocks.Infrastructure;
using Dpm.Identity.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dpm.Identity.Infrastructure.Persistence;

/// <summary>Maps the domain model onto the identity schema defined in db/schema/identity.</summary>
public sealed class UserAccountConfiguration : IEntityTypeConfiguration<UserAccount>
{
    public void Configure(EntityTypeBuilder<UserAccount> builder)
    {
        builder.ToTable("Users", "identity");
        builder.HasKey(u => u.Id);
        builder.Property(u => u.PublicId).IsRequired();
        builder.HasIndex(u => u.PublicId).IsUnique();
        builder.Property(u => u.Email).HasMaxLength(256).IsRequired();
        builder.Property(u => u.EmailNormalized).HasMaxLength(256).IsRequired();
        builder.HasIndex(u => u.EmailNormalized).IsUnique();
        builder.Property(u => u.PasswordHash).HasMaxLength(500);
        builder.Property(u => u.DisplayName).HasMaxLength(120).IsRequired();
        builder.Property(u => u.Status).HasConversion<byte>();
        builder.Property<byte[]>("RowVersion").IsRowVersion();
        builder.HasQueryFilter(u => !u.IsDeleted);

        builder.HasMany(u => u.Roles)
            .WithMany()
            .UsingEntity(
                "UserRoles",
                right => right.HasOne(typeof(Role)).WithMany().HasForeignKey("RoleId"),
                left => left.HasOne(typeof(UserAccount)).WithMany().HasForeignKey("UserId"),
                join =>
                {
                    join.ToTable("UserRoles", "identity");
                    join.HasKey("UserId", "RoleId");
                });

        builder.Navigation(u => u.Roles)
            .UsePropertyAccessMode(PropertyAccessMode.Field)
            .HasField("_roles");

        builder.Ignore(u => u.DomainEvents);
    }
}

public sealed class RoleConfiguration : IEntityTypeConfiguration<Role>
{
    public void Configure(EntityTypeBuilder<Role> builder)
    {
        builder.ToTable("Roles", "identity");
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Name).HasMaxLength(64).IsRequired();
        builder.HasIndex(r => r.Name).IsUnique();
    }
}

public sealed class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        builder.ToTable("RefreshTokens", "identity");
        builder.HasKey(t => t.Id);
        builder.Property(t => t.TokenHash).HasMaxLength(64).IsFixedLength().IsRequired();
        builder.HasIndex(t => t.TokenHash);
        builder.HasIndex(t => t.UserId);
        builder.Property(t => t.ReplacedByHash).HasMaxLength(64).IsFixedLength();
    }
}

public sealed class UserTokenConfiguration : IEntityTypeConfiguration<UserToken>
{
    public void Configure(EntityTypeBuilder<UserToken> builder)
    {
        builder.ToTable("UserTokens", "identity");
        builder.HasKey(t => t.Id);
        builder.Property(t => t.Purpose).HasConversion<byte>();
        builder.Property(t => t.TokenHash).HasMaxLength(64).IsFixedLength().IsRequired();
        builder.HasIndex(t => t.TokenHash);
        builder.HasIndex(t => new { t.UserId, t.Purpose });
    }
}

public sealed class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> builder)
    {
        builder.ToTable("OutboxMessages", "dbo");
        builder.HasKey(m => m.Id);
        builder.Property(m => m.EventId).IsRequired();
        builder.HasIndex(m => m.EventId).IsUnique();
        builder.Property(m => m.Type).HasMaxLength(200).IsRequired();
        builder.Property(m => m.PayloadJson).IsRequired();
    }
}
