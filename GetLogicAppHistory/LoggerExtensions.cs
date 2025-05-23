using LoggerWithOtelExporter.Models;
using Microsoft.Extensions.Logging;

namespace LoggerWithOtelExporter
{
    internal static partial class LoggerExtensions
    {
        [LoggerMessage(LogLevel.Information)]
        public static partial void IntegrationLog(
            this ILogger logger,
            [LogProperties(OmitReferenceName = true)] in LogModel logModel);
    }
}
