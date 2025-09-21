using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Domain.Services.HealthChecking.Services
{
    /// <summary>
    /// 🎯 健康策略计算器工厂 - 支持策略插件化
    /// </summary>
    public interface IHealthPolicyCalculatorFactory
    {
        /// <summary>
        /// 获取指定策略的计算器
        /// </summary>
        IHealthPolicyCalculator GetCalculator(HealthCheckPolicy policy);

        /// <summary>
        /// 注册自定义策略计算器
        /// </summary>
        void RegisterCalculator(IHealthPolicyCalculator calculator);
    }
}