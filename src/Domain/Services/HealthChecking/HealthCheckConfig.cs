using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Domain.Services;

public class HealthCheckConfig : ValueObject
{
    /// <summary>
    /// 是否启用主动健康检查
    /// </summary>
    public bool ActiveEnabled { get; private set; }
    
    /// <summary>
    /// 主动健康检查间隔时间（秒）
    /// </summary>
    public int ActiveIntervalSeconds { get; private set; }
    
    /// <summary>
    /// 主动健康检查超时时间（秒）
    /// </summary>
    public int ActiveTimeoutSeconds { get; private set; }
    
    /// <summary>
    /// 主动健康检查的HTTP路径
    /// </summary>
    public string ActivePath { get; private set; }
    
    /// <summary>
    /// 连续失败次数阈值（超过此值标记为不健康）
    /// </summary>
    public int FailureThreshold { get; private set; }
    
    /// <summary>
    /// 连续成功次数阈值（超过此值标记为健康）
    /// </summary>
    public int SuccessThreshold { get; private set; }

    /// <summary>
    /// 是否启用被动健康检查
    /// </summary>
    public bool PassiveEnabled { get; private set; }
    
    /// <summary>
    /// 被动健康检查失败率阈值（0-1之间）
    /// </summary>
    public double PassiveFailureRateThreshold { get; private set; }
    
    /// <summary>
    /// 被动健康检查最小请求数量（达到此数量才开始统计）
    /// </summary>
    public int PassiveMinRequestCount { get; private set; }
    
    /// <summary>
    /// 被动健康检查恢复检查周期（秒）
    /// </summary>
    public int PassiveReactivationPeriodSeconds { get; private set; }

    /// <summary>
    /// 健康检查策略类型
    /// </summary>
    public HealthCheckPolicy Policy { get; private set; }

    // EF Core 需要的私有无参构造函数
    private HealthCheckConfig()
    {
    }

    // 自定义配置构造函数
    public HealthCheckConfig(
        bool activeEnabled = true,
        int activeIntervalSeconds = 30,
        int activeTimeoutSeconds = 5,
        string activePath = "/health",
        int failureThreshold = 3,
        int successThreshold = 2,
        bool passiveEnabled = false,
        double passiveFailureRateThreshold = 0.1,
        int passiveMinRequestCount = 10,
        int passiveReactivationPeriodSeconds = 120,
        HealthCheckPolicy policy = HealthCheckPolicy.ConsecutiveFailures)
    {
        ValidateAndSetValues(
            activeEnabled, activeIntervalSeconds, activeTimeoutSeconds, activePath,
            failureThreshold, successThreshold, passiveEnabled, passiveFailureRateThreshold,
            passiveMinRequestCount, passiveReactivationPeriodSeconds, policy);
    }

    private void ValidateAndSetValues(
        bool activeEnabled, int activeIntervalSeconds, int activeTimeoutSeconds, string activePath,
        int failureThreshold, int successThreshold, bool passiveEnabled, double passiveFailureRateThreshold,
        int passiveMinRequestCount, int passiveReactivationPeriodSeconds, HealthCheckPolicy policy)
    {
        if (activeIntervalSeconds <= 0)
            throw new ArgumentException("Active interval must be greater than 0", nameof(activeIntervalSeconds));

        if (activeTimeoutSeconds <= 0)
            throw new ArgumentException("Active timeout must be greater than 0", nameof(activeTimeoutSeconds));

        if (activeTimeoutSeconds >= activeIntervalSeconds)
            throw new ArgumentException("Active timeout must be less than interval", nameof(activeTimeoutSeconds));

        if (string.IsNullOrWhiteSpace(activePath))
            throw new ArgumentException("Active path cannot be empty", nameof(activePath));

        if (failureThreshold <= 0)
            throw new ArgumentException("Failure threshold must be greater than 0", nameof(failureThreshold));

        if (successThreshold <= 0)
            throw new ArgumentException("Success threshold must be greater than 0", nameof(successThreshold));

        if (passiveFailureRateThreshold < 0 || passiveFailureRateThreshold > 1)
            throw new ArgumentException("Passive failure rate threshold must be between 0 and 1", nameof(passiveFailureRateThreshold));

        if (passiveMinRequestCount <= 0)
            throw new ArgumentException("Passive min request count must be greater than 0", nameof(passiveMinRequestCount));

        if (passiveReactivationPeriodSeconds <= 0)
            throw new ArgumentException("Passive reactivation period must be greater than 0", nameof(passiveReactivationPeriodSeconds));

        // 赋值
        ActiveEnabled = activeEnabled;
        ActiveIntervalSeconds = activeIntervalSeconds;
        ActiveTimeoutSeconds = activeTimeoutSeconds;
        ActivePath = activePath;
        FailureThreshold = failureThreshold;
        SuccessThreshold = successThreshold;
        PassiveEnabled = passiveEnabled;
        PassiveFailureRateThreshold = passiveFailureRateThreshold;
        PassiveMinRequestCount = passiveMinRequestCount;
        PassiveReactivationPeriodSeconds = passiveReactivationPeriodSeconds;
        Policy = policy;
    }

    public  IEnumerable<object> GetEqualityComponents()
    {
        yield return ActiveEnabled;
        yield return ActiveIntervalSeconds;
        yield return ActiveTimeoutSeconds;
        yield return ActivePath;
        yield return FailureThreshold;
        yield return SuccessThreshold;
        yield return PassiveEnabled;
        yield return PassiveFailureRateThreshold;
        yield return PassiveMinRequestCount;
        yield return PassiveReactivationPeriodSeconds;
        yield return Policy;
    }
}
