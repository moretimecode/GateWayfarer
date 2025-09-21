using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Domain.Routes.Contexts
{
public class ValidationError
{
    public string ErrorMessage { get; }
    public int ErrorCode { get; }
    public string? PropertyName { get; }

    public ValidationError(string errorMessage, int errorCode, string? propertyName = null)
    {
        ErrorMessage = errorMessage ?? throw new ArgumentNullException(nameof(errorMessage));
        ErrorCode = errorCode;
        PropertyName = propertyName;
    }

    public override string ToString()
    {
        return string.IsNullOrEmpty(PropertyName) 
            ? $"[{ErrorCode}] {ErrorMessage}"
            : $"[{ErrorCode}] {PropertyName}: {ErrorMessage}";
    }
}
}