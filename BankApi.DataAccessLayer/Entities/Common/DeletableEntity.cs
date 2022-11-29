namespace BankApi.DataAccessLayer.Entities.Common;

public abstract class DeletableEntity : BaseEntity
{
    public bool IsDeleted { get; set; }

    public DateTime? DeletedDate { get; set; }
}