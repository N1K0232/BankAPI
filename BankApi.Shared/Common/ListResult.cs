namespace BankApi.Shared.Common;

public class ListResult<TModel> where TModel : BaseModel
{
    public IEnumerable<TModel> Content { get; set; }

    public int TotalCount { get; set; }

    public bool HasNextPage { get; set; }
}