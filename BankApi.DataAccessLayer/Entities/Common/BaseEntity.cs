namespace BankApi.DataAccessLayer.Entities.Common;

public abstract class BaseEntity
{
    public Guid Id { get; set; }

    public DateTime CreationDate { get; set; }

    public DateTime? UpdatedDate { get; set; }
}