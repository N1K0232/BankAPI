using Microsoft.AspNetCore.Mvc;

namespace BankApi.Controllers.Common;

public abstract class ControllerBase : Microsoft.AspNetCore.Mvc.ControllerBase
{
	protected ControllerBase()
	{
	}


	protected IActionResult CreateResponse() => null;

	protected IActionResult CreateResponse<T>() => null;
}