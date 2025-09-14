//
// MetricsLogger.cs - High-performance metrics collection and CSV logging
// Tracks latency percentiles, throughput, and system performance metrics
//

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using TradingDataVisualization.DataLayer;

namespace TradingDataVisualization.Workers
{
    /// <summary>
    /// High-performance metrics logger for tracking system performance
    /// Calculates percentiles (P50, P95, P99) and logs to CSV files
    /// </summary>
    public class MetricsLogger : IDisposable
    {
        private readonly string _logDirectory;
        private readonly StreamWriter _csvWriter;
        private readonly ConcurrentQueue<PerformanceMetric> _metricsQueue;
        private readonly System.Threading.Timer _flushTimer;
        private readonly System.Threading.Timer _summaryTimer;
        private readonly object _lockObject = new object();

        // Performance counters
        private readonly PerformanceCounter _cpuCounter;
        private readonly PerformanceCounter _memoryCounter;
        private readonly Process _currentProcess;

        // Metrics aggregation
        private readonly List<long> _endToEndLatencies = new List<long>();
        private readonly List<long> _processingLatencies = new List<long>();
        private readonly List<long> _renderLatencies = new List<long>();
        private long _totalMessages;
        private long _totalThroughput;
        private DateTime _startTime = DateTime.UtcNow;
        private DateTime _lastSummaryTime = DateTime.UtcNow;

        // GC tracking
        private long _lastGen0Collections;
        private long _lastGen1Collections;
        private long _lastGen2Collections;

        // UI render timing tracking
        private long _lastRenderStartTime;
        private long _lastRenderEndTime;

        public MetricsLogger(string logDirectory = "Logs")
        {
            _logDirectory = logDirectory;
            Directory.CreateDirectory(_logDirectory);

            var csvPath = Path.Combine(_logDirectory, $"metrics_{DateTime.Now:yyyyMMdd_HHmmss}.csv");
            _csvWriter = new StreamWriter(csvPath, false, Encoding.UTF8) { AutoFlush = false };

            // Write CSV header
            _csvWriter.WriteLine("Timestamp,MessageId,MessageType,SendTimestamp,ReceiveTimestamp," +
                "QueueTimestamp,ApplyTimestamp,RenderStartTimestamp,RenderEndTimestamp," +
                "EndToEndLatency,ProcessingLatency,RenderLatency,QueueDepth,UIRenderQueueDepth," +
                "CPUUsage,MemoryUsage,Gen0Collections,Gen1Collections,Gen2Collections");

            _metricsQueue = new ConcurrentQueue<PerformanceMetric>();
            _currentProcess = Process.GetCurrentProcess();

            // Initialize performance counters
            try
            {
                _cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total");
                _memoryCounter = new PerformanceCounter("Memory", "Available MBytes");
                _cpuCounter.NextValue(); // Prime the counter
            }
            catch
            {
                // Performance counters might not be available
            }

            // Setup timers
            _flushTimer = new System.Threading.Timer(FlushMetrics, null, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1));
            _summaryTimer = new System.Threading.Timer(WriteSummaryMetrics, null, TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(10));

            _lastGen0Collections = GC.CollectionCount(0);
            _lastGen1Collections = GC.CollectionCount(1);
            _lastGen2Collections = GC.CollectionCount(2);
        }

        /// <summary>
        /// Updates render timing information for metrics tracking
        /// </summary>
        public void UpdateRenderTiming(long renderStart, long renderEnd)
        {
            _lastRenderStartTime = renderStart;
            _lastRenderEndTime = renderEnd;
        }

        /// <summary>
        /// Logs performance metrics for a processed message
        /// </summary>
        public async Task LogMessageMetricsAsync(DataMessage message)
        {
            // Set render timestamps if not already set
            if (message.RenderStartTimestamp == 0)
                message.RenderStartTimestamp = _lastRenderStartTime;
            if (message.RenderEndTimestamp == 0)
                message.RenderEndTimestamp = _lastRenderEndTime;

            var metric = new PerformanceMetric
            {
                Timestamp = DateTime.UtcNow,
                MessageType = message.Type.ToString(),
                SendTimestamp = message.SendTimestamp,
                ReceiveTimestamp = message.ReceiveTimestamp,
                QueueTimestamp = message.QueueTimestamp,
                ApplyTimestamp = message.ApplyTimestamp,
                RenderStartTimestamp = message.RenderStartTimestamp,
                RenderEndTimestamp = message.RenderEndTimestamp,
                EndToEndLatency = message.GetEndToEndLatency(),
                ProcessingLatency = message.GetProcessingLatency(),
                RenderLatency = message.GetRenderLatency()
            };

            _metricsQueue.Enqueue(metric);

            // Update aggregated metrics
            lock (_lockObject)
            {
                _endToEndLatencies.Add(metric.EndToEndLatency);
                _processingLatencies.Add(metric.ProcessingLatency);
                _renderLatencies.Add(metric.RenderLatency);
                _totalMessages++;

                // Keep only recent latencies for percentile calculation (last 10,000 messages)
                if (_endToEndLatencies.Count > 10000)
                {
                    _endToEndLatencies.RemoveAt(0);
                    _processingLatencies.RemoveAt(0);
                    _renderLatencies.RemoveAt(0);
                }
            }
        }

        /// <summary>
        /// Logs an error message with timestamp
        /// </summary>
        public void LogError(string message)
        {
            var errorPath = Path.Combine(_logDirectory, $"errors_{DateTime.Now:yyyyMMdd}.log");
            var logEntry = $"{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss.fff} ERROR: {message}";

            try
            {
                File.AppendAllText(errorPath, logEntry + Environment.NewLine);
            }
            catch
            {
                // Ignore logging errors to prevent cascading failures
            }
        }

        /// <summary>
        /// Logs an informational message
        /// </summary>
        public void LogInfo(string message)
        {
            var infoPath = Path.Combine(_logDirectory, $"info_{DateTime.Now:yyyyMMdd}.log");
            var logEntry = $"{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss.fff} INFO: {message}";

            try
            {
                File.AppendAllText(infoPath, logEntry + Environment.NewLine);
            }
            catch
            {
                // Ignore logging errors
            }
        }

        /// <summary>
        /// Gets current performance statistics
        /// </summary>
        public PerformanceStats GetCurrentStats()
        {
            lock (_lockObject)
            {
                var stats = new PerformanceStats
                {
                    TotalMessages = _totalMessages,
                    UptimeMs = (long)(DateTime.UtcNow - _startTime).TotalMilliseconds,
                    QueueDepth = _metricsQueue.Count
                };

                if (_endToEndLatencies.Count > 0)
                {
                    var sortedE2E = _endToEndLatencies.OrderBy(x => x).ToArray();
                    var sortedProcessing = _processingLatencies.OrderBy(x => x).ToArray();
                    var sortedRender = _renderLatencies.OrderBy(x => x).ToArray();

                    stats.EndToEndLatencyP50 = CalculatePercentile(sortedE2E, 50);
                    stats.EndToEndLatencyP95 = CalculatePercentile(sortedE2E, 95);
                    stats.EndToEndLatencyP99 = CalculatePercentile(sortedE2E, 99);

                    stats.ProcessingLatencyP50 = CalculatePercentile(sortedProcessing, 50);
                    stats.ProcessingLatencyP95 = CalculatePercentile(sortedProcessing, 95);
                    stats.ProcessingLatencyP99 = CalculatePercentile(sortedProcessing, 99);

                    stats.RenderLatencyP50 = CalculatePercentile(sortedRender, 50);
                    stats.RenderLatencyP95 = CalculatePercentile(sortedRender, 95);
                    stats.RenderLatencyP99 = CalculatePercentile(sortedRender, 99);
                }

                // Calculate throughput (messages per second)
                var elapsedSeconds = Math.Max(1, (DateTime.UtcNow - _startTime).TotalSeconds);
                stats.ThroughputMsgPerSec = _totalMessages / elapsedSeconds;

                // Get system metrics
                try
                {
                    stats.CpuUsagePercent = _cpuCounter?.NextValue() ?? 0;
                    stats.AvailableMemoryMB = _memoryCounter?.NextValue() ?? 0;
                    stats.ProcessMemoryMB = _currentProcess.WorkingSet64 / (1024 * 1024);
                }
                catch
                {
                    // Ignore performance counter errors
                }

                // Get GC metrics
                var currentGen0 = GC.CollectionCount(0);
                var currentGen1 = GC.CollectionCount(1);
                var currentGen2 = GC.CollectionCount(2);

                stats.Gen0Collections = currentGen0 - _lastGen0Collections;
                stats.Gen1Collections = currentGen1 - _lastGen1Collections;
                stats.Gen2Collections = currentGen2 - _lastGen2Collections;

                return stats;
            }
        }

        /// <summary>
        /// Calculates percentile value from sorted array
        /// </summary>
        private long CalculatePercentile(long[] sortedValues, int percentile)
        {
            if (sortedValues.Length == 0) return 0;

            double index = (percentile / 100.0) * (sortedValues.Length - 1);
            int lowerIndex = (int)Math.Floor(index);
            int upperIndex = (int)Math.Ceiling(index);

            if (lowerIndex == upperIndex)
                return sortedValues[lowerIndex];

            double weight = index - lowerIndex;
            return (long)(sortedValues[lowerIndex] * (1 - weight) + sortedValues[upperIndex] * weight);
        }

        /// <summary>
        /// Flushes queued metrics to CSV file
        /// </summary>
        private void FlushMetrics(object state)
        {
            try
            {
                var metricsToWrite = new List<PerformanceMetric>();
                while (_metricsQueue.TryDequeue(out var metric))
                {
                    metricsToWrite.Add(metric);
                }

                if (metricsToWrite.Count == 0) return;

                var stats = GetCurrentStats();

                foreach (var metric in metricsToWrite)
                {
                    _csvWriter.WriteLine($"{metric.Timestamp:yyyy-MM-dd HH:mm:ss.fff}," +
                        $"{metric.MessageType},{metric.SendTimestamp},{metric.ReceiveTimestamp}," +
                        $"{metric.QueueTimestamp},{metric.ApplyTimestamp}," +
                        $"{metric.RenderStartTimestamp},{metric.RenderEndTimestamp}," +
                        $"{metric.EndToEndLatency},{metric.ProcessingLatency},{metric.RenderLatency}," +
                        $"{stats.QueueDepth},{stats.UiRenderQueueDepth},{stats.CpuUsagePercent:F1},{stats.ProcessMemoryMB}," +
                        $"{stats.Gen0Collections},{stats.Gen1Collections},{stats.Gen2Collections}");
                }

                _csvWriter.Flush();
            }
            catch (Exception ex)
            {
                LogError($"Error flushing metrics: {ex.Message}");
            }
        }

        /// <summary>
        /// Writes summary statistics to CSV
        /// </summary>
        private void WriteSummaryMetrics(object state)
        {
            try
            {
                var stats = GetCurrentStats();
                var now = DateTime.UtcNow;
                var intervalSeconds = (now - _lastSummaryTime).TotalSeconds;
                var intervalThroughput = (_totalMessages - _totalThroughput) / intervalSeconds;

                // Write summary line to CSV
                _csvWriter.WriteLine($"# SUMMARY {now:yyyy-MM-dd HH:mm:ss.fff}: " +
                    $"Messages={stats.TotalMessages}, " +
                    $"Throughput={stats.ThroughputMsgPerSec:F1}/sec, " +
                    $"IntervalThroughput={intervalThroughput:F1}/sec, " +
                    $"E2E_Latency_P50={stats.EndToEndLatencyP50}ms, " +
                    $"E2E_Latency_P95={stats.EndToEndLatencyP95}ms, " +
                    $"E2E_Latency_P99={stats.EndToEndLatencyP99}ms, " +
                    $"Processing_P50={stats.ProcessingLatencyP50}ms, " +
                    $"Processing_P95={stats.ProcessingLatencyP95}ms, " +
                    $"Processing_P99={stats.ProcessingLatencyP99}ms, " +
                    $"Render_P50={stats.RenderLatencyP50}ms, " +
                    $"Render_P95={stats.RenderLatencyP95}ms, " +
                    $"Render_P99={stats.RenderLatencyP99}ms, " +
                    $"CPU={stats.CpuUsagePercent:F1}%, " +
                    $"Memory={stats.ProcessMemoryMB}MB, " +
                    $"UIQueue={stats.UiRenderQueueDepth}, " +
                    $"GC_Gen0={stats.Gen0Collections}, " +
                    $"GC_Gen1={stats.Gen1Collections}, " +
                    $"GC_Gen2={stats.Gen2Collections}");

                _csvWriter.Flush();

                _lastSummaryTime = now;
                _totalThroughput = _totalMessages;

                // Update GC baselines
                _lastGen0Collections = GC.CollectionCount(0);
                _lastGen1Collections = GC.CollectionCount(1);
                _lastGen2Collections = GC.CollectionCount(2);

                // Log to console for monitoring
                Console.WriteLine($"[{now:HH:mm:ss}] Messages: {stats.TotalMessages:N0}, " +
                    $"Throughput: {stats.ThroughputMsgPerSec:F1}/sec, " +
                    $"E2E P99: {stats.EndToEndLatencyP99}ms, " +
                    $"Memory: {stats.ProcessMemoryMB}MB");
            }
            catch (Exception ex)
            {
                LogError($"Error writing summary metrics: {ex.Message}");
            }
        }

        public void Dispose()
        {
            _flushTimer?.Dispose();
            _summaryTimer?.Dispose();

            // Final flush
            FlushMetrics(null);
            WriteSummaryMetrics(null);

            _csvWriter?.Dispose();
            _cpuCounter?.Dispose();
            _memoryCounter?.Dispose();
            _currentProcess?.Dispose();
        }
    }

    /// <summary>
    /// Individual performance metric for a single message
    /// </summary>
    public class PerformanceMetric
    {
        public DateTime Timestamp { get; set; }
        public string MessageType { get; set; } = string.Empty;
        public long SendTimestamp { get; set; }
        public long ReceiveTimestamp { get; set; }
        public long QueueTimestamp { get; set; }
        public long ApplyTimestamp { get; set; }
        public long RenderStartTimestamp { get; set; }
        public long RenderEndTimestamp { get; set; }
        public long EndToEndLatency { get; set; }
        public long ProcessingLatency { get; set; }
        public long RenderLatency { get; set; }
    }

    /// <summary>
    /// Aggregated performance statistics
    /// </summary>
    public class PerformanceStats
    {
        public long TotalMessages { get; set; }
        public double ThroughputMsgPerSec { get; set; }
        public long UptimeMs { get; set; }
        public int QueueDepth { get; set; }
        public int UiRenderQueueDepth { get; set; }

        // Latency percentiles (in milliseconds)
        public long EndToEndLatencyP50 { get; set; }
        public long EndToEndLatencyP95 { get; set; }
        public long EndToEndLatencyP99 { get; set; }

        public long ProcessingLatencyP50 { get; set; }
        public long ProcessingLatencyP95 { get; set; }
        public long ProcessingLatencyP99 { get; set; }

        public long RenderLatencyP50 { get; set; }
        public long RenderLatencyP95 { get; set; }
        public long RenderLatencyP99 { get; set; }

        // System metrics
        public float CpuUsagePercent { get; set; }
        public float AvailableMemoryMB { get; set; }
        public long ProcessMemoryMB { get; set; }

        // GC metrics
        public long Gen0Collections { get; set; }
        public long Gen1Collections { get; set; }
        public long Gen2Collections { get; set; }

        public string GetFormattedStats()
        {
            return $"Messages: {TotalMessages:N0}, Throughput: {ThroughputMsgPerSec:F1}/sec, " +
                   $"E2E Latency P99: {EndToEndLatencyP99}ms, CPU: {CpuUsagePercent:F1}%, " +
                   $"Memory: {ProcessMemoryMB}MB, Queue: {QueueDepth}, UI Queue: {UiRenderQueueDepth}";
        }
    }
}
