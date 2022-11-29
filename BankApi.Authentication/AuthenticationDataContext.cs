using System.Reflection;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Microsoft.Extensions.Logging;

namespace BankApi.Authentication;

public class AuthenticationDataContext : IdentityDbContext
{
    private readonly ValueConverter<string, string> trimStringConverter = new(v => v.Trim(), v => v.Trim());
    private readonly ILogger<AuthenticationDataContext> logger;

    public AuthenticationDataContext(DbContextOptions options, ILogger<AuthenticationDataContext> logger) : base(options)
    {
        this.logger = logger;
    }


    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var savedEntries = await base.SaveChangesAsync(cancellationToken);
            logger.LogInformation($"saved entries: {savedEntries}");

            return savedEntries;
        }
        catch (DbUpdateConcurrencyException ex)
        {
            logger.LogError(ex, ex.Message);
            throw ex;
        }
        catch (DbUpdateException ex)
        {
            logger.LogError(ex, ex.Message);
            throw ex;
        }
    }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());

        foreach (var entity in builder.Model.GetEntityTypes())
        {
            foreach (var property in entity.GetProperties())
            {
                if (property.ClrType == typeof(string))
                {
                    builder.Entity(entity.Name)
                        .Property(property.Name)
                        .HasConversion(trimStringConverter);
                }
            }
        }
    }
}