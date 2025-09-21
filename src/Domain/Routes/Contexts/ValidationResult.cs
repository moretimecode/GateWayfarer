using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Domain.Routes.Contexts
{
public class ValidationResult
{
    public bool IsValid { get; private set; }
    public List<ValidationError> Errors { get; private set; }

    private ValidationResult(bool isValid, List<ValidationError> errors)
    {
        IsValid = isValid;
        Errors = errors ?? new List<ValidationError>();
    }

    // 创建成功的验证结果
    public static ValidationResult Success()
    {
        return new ValidationResult(true, new List<ValidationError>());
    }

    // 创建失败的验证结果
    public static ValidationResult Failure(string errorMessage, int errorCode, string? propertyName = null)
    {
        var error = new ValidationError(errorMessage, errorCode, propertyName);
        return new ValidationResult(false, new List<ValidationError> { error });
    }

    // 创建失败的验证结果
    public static ValidationResult Failure(List<ValidationError> errors)
    {
        return new ValidationResult(false, errors);
    }

    // 添加错误
    public ValidationResult AddError(string errorMessage, int errorCode, string? propertyName = null)
    {
        var error = new ValidationError(errorMessage, errorCode, propertyName);
        Errors.Add(error);
        IsValid = false;
        return this;
    }

    // 合并验证结果
    public ValidationResult Combine(ValidationResult other)
    {
        if (other.IsValid)
            return this;

        Errors.AddRange(other.Errors);
        IsValid = false;
        return this;
    }

    // 获取第一个错误消息
    public string GetFirstErrorMessage()
    {
        return Errors.FirstOrDefault()?.ErrorMessage ?? string.Empty;
    }

    // 获取所有错误消息
    public List<string> GetAllErrorMessages()
    {
        return Errors.Select(e => e.ErrorMessage).ToList();
    }

    // 检查是否包含特定错误代码
    public bool HasErrorCode(int errorCode)
    {
        return Errors.Any(e => e.ErrorCode == errorCode);
    }
}
}