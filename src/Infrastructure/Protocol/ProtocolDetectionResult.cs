namespace Infrastructure.Protocol;

/// <summary>
/// 协议检测结果
/// 包含检测状态和详细信息
/// </summary>
public record ProtocolDetectionResult
{
    /// <summary>
    /// 是否检测成功
    /// </summary>
    public bool IsSuccess { get; init; }

    /// <summary>
    /// 检测到的协议信息
    /// </summary>
    public ProtocolInfo? ProtocolInfo { get; init; }

    /// <summary>
    /// 错误信息（检测失败时）
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// 检测过程中的警告信息
    /// </summary>
    public IReadOnlyList<string> Warnings { get; init; } = [];

    /// <summary>
    /// 检测耗时
    /// </summary>
    public TimeSpan DetectionTime { get; init; }

    /// <summary>
    /// 创建成功结果
    /// </summary>
    public static ProtocolDetectionResult Success(ProtocolInfo protocolInfo, TimeSpan detectionTime = default, IReadOnlyList<string>? warnings = null)
    {
        return new ProtocolDetectionResult
        {
            IsSuccess = true,
            ProtocolInfo = protocolInfo ?? throw new ArgumentNullException(nameof(protocolInfo)),
            DetectionTime = detectionTime,
            Warnings = warnings ?? []
        };
    }

    /// <summary>
    /// 创建失败结果
    /// </summary>
    public static ProtocolDetectionResult Failure(string errorMessage, TimeSpan detectionTime = default)
    {
        return new ProtocolDetectionResult
        {
            IsSuccess = false,
            ErrorMessage = errorMessage ?? throw new ArgumentNullException(nameof(errorMessage)),
            DetectionTime = detectionTime
        };
    }

    /// <summary>
    /// 创建带警告的成功结果
    /// </summary>
    public static ProtocolDetectionResult SuccessWithWarnings(ProtocolInfo protocolInfo, IReadOnlyList<string> warnings, TimeSpan detectionTime = default)
    {
        return new ProtocolDetectionResult
        {
            IsSuccess = true,
            ProtocolInfo = protocolInfo ?? throw new ArgumentNullException(nameof(protocolInfo)),
            Warnings = warnings ?? throw new ArgumentNullException(nameof(warnings)),
            DetectionTime = detectionTime
        };
    }
}