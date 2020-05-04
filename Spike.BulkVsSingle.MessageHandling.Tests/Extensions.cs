using System;

namespace Spike.BulkVsSingle.MessageHandling.Tests
{
    public static class Extensions
    {
        public static TimeSpan GetTimeToWait(this Configuration.Configuration config) =>
            TimeSpan.Parse(config.Config.GetSection("AppSettings")["TimeToWait"]);

        public static string GetNSBServiceEndpointName(this Configuration.Configuration config) =>
            config.Config.GetSection("AppSettings")["NSBServiceEndpointName"];
        public static string GetBatchServiceEndpointName(this Configuration.Configuration config) =>
            config.Config.GetSection("AppSettings")["BatchServiceEndpointName"];
    }
}