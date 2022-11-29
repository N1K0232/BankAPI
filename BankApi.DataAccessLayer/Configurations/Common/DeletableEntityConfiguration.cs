using BankApi.DataAccessLayer.Entities.Common;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BankApi.DataAccessLayer.Configurations.Common;

public abstract class DeletableEntityConfiguration<TEntity> : BaseEntityConfiguration<TEntity> where TEntity : DeletableEntity
{
    public override void Configure(EntityTypeBuilder<TEntity> builder)
    {
        builder.Property(x => x.IsDeleted).IsRequired();
        builder.Property(x => x.DeletedDate).IsRequired(false);

        base.Configure(builder);
    }
}