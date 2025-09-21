namespace Domain.Routes;

public enum LoadBalanceType
{
    /// <summary>
    /// 轮询
    /// </summary>
    RoundRobin = 0,
    
    /// <summary>
    /// 加权轮询  
    /// </summary>
    WeightedRoundRobin = 1,
    
    /// <summary>
    /// 随机
    /// </summary>
    Random = 2,
    WeightedRandom = 3,
    LeastConnections = 4,
    IpHash = 5,
    //一致性哈希
    ConsistentHash=6
}