namespace Zakupki.Fetcher.Options;

public sealed class EventBusOptions
{
    public bool Enabled { get; set; }

    public string Broker { get; set; } = string.Empty;

    public int RetryCount { get; set; } = 5;

    public string QueueName { get; set; } = string.Empty;

    public string CommandQueueName { get; set; } = string.Empty;

    public string ExchangeType { get; set; } = "direct";

    public int InFlightDeduplicationMinutes { get; set; } = 30;

    public BusAccessOptions BusAccess { get; set; } = new();

    public sealed class BusAccessOptions
    {
        public string Host { get; set; } = "localhost";

        public string UserName { get; set; } = "guest";

        public string Password { get; set; } = "guest";

        public int RetryCount { get; set; } = 5;
    }
}
