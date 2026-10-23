using FluentValidation.Results;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace Catalog.Api.Controllers;

internal static class ValidationResultExtensions
{
    public static ModelStateDictionary AddTo(this ValidationResult result, ModelStateDictionary modelState)
    {
        foreach (var error in result.Errors)
            modelState.AddModelError(error.PropertyName, error.ErrorMessage);

        return modelState;
    }
}
