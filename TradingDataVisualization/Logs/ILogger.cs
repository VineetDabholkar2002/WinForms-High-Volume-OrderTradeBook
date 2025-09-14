//
// ILogger.cs - Centralized logging interface for dependency injection
//

using System;

namespace TradingDataVisualization.Logs
{
    /// <summary>
    /// Centralized logging interface for dependency injection across all application components.
    /// Provides consistent logging methods with different severity levels.
    /// </summary>
    public interface ILogger
    {
        /// <summary>
        /// Logs informational messages for application flow and status updates.
        /// </summary>
        /// <param name="message">The information message to log</param>
        void LogInformation(string message);

        /// <summary>
        /// Logs error messages with optional exception details.
        /// </summary>
        /// <param name="message">The error message to log</param>
        /// <param name="ex">Optional exception to include in the log</param>
        void LogError(string message, Exception ex = null);

        /// <summary>
        /// Logs warning messages for potential issues.
        /// </summary>
        /// <param name="message">The warning message to log</param>
        void LogWarning(string message);

        /// <summary>
        /// Logs debug messages for detailed troubleshooting.
        /// </summary>
        /// <param name="message">The debug message to log</param>
        void LogDebug(string message);
    }

    /// <summary>
    /// Simple console-based logger implementation.
    /// Can be replaced with more sophisticated logging frameworks like Serilog, NLog, etc.
    /// </summary>
    public class ConsoleLogger : ILogger
    {
        private readonly string _componentName;

        public ConsoleLogger(string componentName = "Application")
        {
            _componentName = componentName;
        }

        public void LogInformation(string message)
        {
            Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [INFO] [{_componentName}] {message}");
        }

        public void LogError(string message, Exception ex = null)
        {
            Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [ERROR] [{_componentName}] {message}");
            if (ex != null)
            {
                Console.WriteLine($"Exception: {ex.Message}");
                Console.WriteLine($"StackTrace: {ex.StackTrace}");
            }
        }

        public void LogWarning(string message)
        {
            Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [WARN] [{_componentName}] {message}");
        }

        public void LogDebug(string message)
        {
            Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [DEBUG] [{_componentName}] {message}");
        }
    }
}