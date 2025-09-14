//
// AppSettings.cs - Configuration settings for the application
//

using System;

namespace TradingDataVisualization.Infrastructure
{
    /// <summary>
    /// Application configuration settings
    /// </summary>
    public class AppSettings
    {
        /// <summary>
        /// TCP port for data ingestion
        /// </summary>
        public int TcpPort { get; set; } = 9999;
        
        /// <summary>
        /// Named pipe name for local communication
        /// </summary>
        public string NamedPipeName { get; set; } = "TradingDataPipe";
        
        /// <summary>
        /// Maximum batch size for processing messages
        /// </summary>
        public int BatchSize { get; set; } = 1000;
        
        /// <summary>
        /// Maximum time to wait before processing a partial batch (milliseconds)
        /// </summary>
        public int BatchTimeoutMs { get; set; } = 100;
        
        /// <summary>
        /// UI refresh rate cap (frames per second)
        /// </summary>
        public int MaxRefreshRateFps { get; set; } = 60;
        public int UiUpdateIntervalMs
        {
            get => (int)(1000.0 / MaxRefreshRateFps);
        }

        /// <summary>
        /// Maximum number of search results to return
        /// </summary>
        public int MaxSearchResults { get; set; } = 1000000;
        
        /// <summary>
        /// Enable performance monitoring
        /// </summary>
        public bool EnableMetrics { get; set; } = true;
        
        /// <summary>
        /// Log directory for metrics and logs
        /// </summary>
        public string LogDirectory { get; set; } = "Logs";
        
        /// <summary>
        /// TCP buffer size for network operations
        /// </summary>
        public int TcpBufferSize { get; set; } = 8192;
        
        
        /// <summary>
        /// Validates configuration settings
        /// </summary>
        public void Validate()
        {
            if (TcpPort <= 0 || TcpPort > 65535)
                throw new ArgumentException("TCP port must be between 1 and 65535");
            
            if (string.IsNullOrWhiteSpace(NamedPipeName))
                throw new ArgumentException("Named pipe name cannot be empty");
            
            if (BatchSize <= 0 || BatchSize > 10000)
                throw new ArgumentException("Batch size must be between 1 and 10000");
            
            if (BatchTimeoutMs <= 0 || BatchTimeoutMs > 10000)
                throw new ArgumentException("Batch timeout must be between 1 and 10000 ms");
            
            if (MaxRefreshRateFps <= 0 || MaxRefreshRateFps > 120)
                throw new ArgumentException("Max refresh rate must be between 1 and 120 FPS");
        }
        
        /// <summary>
        /// Gets formatted configuration summary
        /// </summary>
        public string GetConfigSummary()
        {
            return $"TCP Port: {TcpPort}, Pipe: {NamedPipeName}, " +
                   $"Batch: {BatchSize}/{BatchTimeoutMs}ms, " +
                   $"Refresh: {MaxRefreshRateFps}fps, " +
                   $"Metrics: {EnableMetrics}";
        }
    }
}