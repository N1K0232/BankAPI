using System.Linq.Expressions;
using System.Reflection;
using BankApi.Authentication;
using BankApi.DataAccessLayer.Entities.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.Extensions.Logging;

namespace BankApi.DataAccessLayer;

public class ApplicationDataContext : AuthenticationDataContext, IDataContext
{
    public ApplicationDataContext(DbContextOptions<ApplicationDataContext> options, ILogger<ApplicationDataContext> logger) : base(options, logger)
    {
    }


    public void Delete<TEntity>(TEntity entity) where TEntity : BaseEntity
    {
        var set = Set<TEntity>();
        set.Remove(entity);
    }

    public void Delete<TEntity>(IEnumerable<TEntity> entities) where TEntity : BaseEntity
    {
        var set = Set<TEntity>();
        set.RemoveRange(entities);
    }

    public void Create<TEntity>(TEntity entity) where TEntity : BaseEntity
    {
        var set = Set<TEntity>();
        set.Add(entity);
    }

    public void Edit<TEntity>(TEntity entity) where TEntity : BaseEntity
    {
        var set = Set<TEntity>();
        set.Update(entity);
    }

    public Task<bool> ExistsAsync<TEntity>(Guid id) where TEntity : BaseEntity
    {
        return ExistsInternalAsync<TEntity>(x => x.Id == id);
    }

    public Task<bool> ExistsAsync<TEntity>(Expression<Func<TEntity, bool>> predicate) where TEntity : BaseEntity
    {
        return ExistsInternalAsync<TEntity>(predicate);
    }

    public Task<TEntity> GetAsync<TEntity>(Guid id) where TEntity : BaseEntity
    {
        var set = Set<TEntity>();
        return set.FindAsync(id).AsTask();
    }

    public IQueryable<TEntity> GetData<TEntity>(bool ignoreQueryFilters = false, bool trackingChanges = false) where TEntity : BaseEntity
    {
        return GetDataInternal<TEntity>(ignoreQueryFilters, trackingChanges);
    }

    public Task<int> SaveAsync() => SaveChangesAsync();

#pragma warning disable IDE0007 //Use implicit type
    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        var entries = GetEntries();

        foreach (var entry in entries)
        {
            BaseEntity baseEntity = entry.Entity as BaseEntity;

            if (entry.State is EntityState.Added)
            {
                if (baseEntity is DeletableEntity deletableEntity)
                {
                    deletableEntity.IsDeleted = false;
                    deletableEntity.DeletedDate = null;
                }

                baseEntity.CreationDate = DateTime.UtcNow;
                baseEntity.UpdatedDate = null;
            }

            if (entry.State is EntityState.Modified)
            {
                if (baseEntity is DeletableEntity deletableEntity)
                {
                    deletableEntity.IsDeleted = false;
                    deletableEntity.DeletedDate = null;
                }

                baseEntity.UpdatedDate = DateTime.UtcNow;
            }

            if (entry.State is EntityState.Deleted)
            {
                if (baseEntity is DeletableEntity deletableEntity)
                {
                    entry.State = EntityState.Modified;
                    deletableEntity.IsDeleted = true;
                    deletableEntity.DeletedDate = DateTime.UtcNow;
                }
            }
        }

        return base.SaveChangesAsync(cancellationToken);
    }
#pragma warning restore IDE0007 //Use implicit type

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());
    }

    private IEnumerable<EntityEntry> GetEntries()
    {
        var entries = ChangeTracker.Entries()
            .Where(e => e.Entity.GetType().IsSubclassOf(typeof(BaseEntity))).ToList();

        return entries.Where(e => e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted);
    }

    private IQueryable<TEntity> GetDataInternal<TEntity>(bool ignoreQueryFilters = false, bool trackingChanges = false) where TEntity : BaseEntity
    {
        var set = Set<TEntity>().AsQueryable();

        if (ignoreQueryFilters)
        {
            set = set.IgnoreQueryFilters();
        }

        return trackingChanges ?
            set.AsTracking() :
            set.AsNoTrackingWithIdentityResolution();
    }

    private Task<bool> ExistsInternalAsync<TEntity>(Expression<Func<TEntity, bool>> predicate) where TEntity : BaseEntity
    {
        var set = GetDataInternal<TEntity>();
        return set.AnyAsync(predicate);
    }
}