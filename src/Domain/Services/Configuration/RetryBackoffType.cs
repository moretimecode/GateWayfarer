namespace Domain.Services.Configuration
{
    public enum RetryBackoffType
    {
        Fixed,
        Linear,
        Exponential,
        ExponentialWithJitter
    }
}