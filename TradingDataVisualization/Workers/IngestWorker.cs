//
// IngestWorker.cs - Background worker for high-performance data ingestion
// Handles TCP/Named Pipe connections with batching and coalescing
//
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using TradingDataVisualization.DataLayer;
using TradingDataVisualization.Infrastructure;
using TradingDataVisualization.Logs;

namespace TradingDataVisualization.Workers
{
    /// <summary>
    /// High-performance background worker for ingesting trading data
    /// Supports TCP sockets and Named Pipes with configurable batching
    /// </summary>
    public class IngestWorker : IDisposable
    {
        private readonly Channel<DataMessage> _incomingChannel;
        private readonly ChannelWriter<DataMessage> _channelWriter;
        private readonly ChannelReader<DataMessage> _channelReader;
        private readonly ColumnOrientedDataStore<OrderBookEntry> _orderBookStore;
        private readonly ColumnOrientedDataStore<TradeBookEntry> _tradeBookStore;
        private readonly MetricsLogger _metricsLogger;
        private readonly AppSettings _settings;
        private readonly ILogger _logger;
        private readonly CancellationTokenSource _cancellationSource;
        private TcpListener _tcpListener;
        private NamedPipeServerStream _pipeServer;
        private Task _tcpTask;
        private Task _pipeTask;
        private Task _processingTask;
        private volatile bool _isRunning;
        // Performance tracking
        private readonly ConcurrentQueue<DataMessage> _batchQueue;
        private volatile int _messagesReceived;
        private volatile int _messagesProcessed;
        private DateTime _lastBatchTime;

        /// <summary>
        /// Event fired when data is processed and ready for UI update
        /// </summary>
        public event EventHandler<DataBatchProcessedEventArgs> DataBatchProcessed;

        /// <summary>
        /// Current statistics for monitoring
        /// </summary>
        public IngestStats GetStats()
        {
            return new IngestStats
            {
                MessagesReceived = _messagesReceived,
                MessagesProcessed = _messagesProcessed,
                QueueDepth = _batchQueue.Count,
                IsRunning = _isRunning,
                OrderBookRows = _orderBookStore.RowCount,
                TradeBookRows = _tradeBookStore.RowCount
            };
        }

        public IngestWorker(
            ColumnOrientedDataStore<OrderBookEntry> orderBookStore,
            ColumnOrientedDataStore<TradeBookEntry> tradeBookStore,
            MetricsLogger metricsLogger,
            AppSettings settings,
            ILogger logger)
        {
            _orderBookStore = orderBookStore ?? throw new ArgumentNullException(nameof(orderBookStore));
            _tradeBookStore = tradeBookStore ?? throw new ArgumentNullException(nameof(tradeBookStore));
            _metricsLogger = metricsLogger ?? throw new ArgumentNullException(nameof(metricsLogger));
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            // Create high-performance channel for message passing
            var channelOptions = new UnboundedChannelOptions
            {
                SingleReader = true,
                SingleWriter = false,
                AllowSynchronousContinuations = false
            };
            _incomingChannel = Channel.CreateUnbounded<DataMessage>(channelOptions);
            _channelWriter = _incomingChannel.Writer;
            _channelReader = _incomingChannel.Reader;
            _cancellationSource = new CancellationTokenSource();
            _batchQueue = new ConcurrentQueue<DataMessage>();
            _lastBatchTime = DateTime.UtcNow;
            _logger.LogInformation("IngestWorker initialized with TCP and Named Pipe support");
        }

        /// <summary>
        /// Starts the ingestion worker with TCP and Named Pipe listeners
        /// </summary>
        public async Task StartAsync()
        {
            if (_isRunning) return;
            _isRunning = true;
            _logger.LogInformation("Starting data ingestion worker");
            try
            {
                // Start TCP listener
                _tcpTask = StartTcpListenerAsync(_cancellationSource.Token);
                // Start Named Pipe listener
                _pipeTask = StartNamedPipeListenerAsync(_cancellationSource.Token);
                // Start message processing task
                _processingTask = StartMessageProcessingAsync(_cancellationSource.Token);
                await Task.Delay(100); // Allow listeners to start
                _logger.LogInformation("Data ingestion worker started successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError("Failed to start ingestion worker", ex);
                throw;
            }
        }

        /// <summary>
        /// Stops the ingestion worker gracefully
        /// </summary>
        public async Task StopAsync()
        {
            if (!_isRunning) return;
            _logger.LogInformation("Stopping data ingestion worker");
            _isRunning = false;
            _cancellationSource.Cancel();
            try
            {
                // Close listeners
                try { _tcpListener?.Stop(); } catch { }
                try { _pipeServer?.Close(); } catch { }
                // Complete the channel
                _channelWriter.Complete();
                // Wait for tasks to complete
                var tasks = new[] { _tcpTask, _pipeTask, _processingTask }.Where(t => t != null);
                await Task.WhenAll(tasks).ConfigureAwait(false);
                _logger.LogInformation("Data ingestion worker stopped successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError("Error during ingestion worker shutdown", ex);
                throw;
            }
        }

        /// <summary>
        /// TCP listener for high-performance message ingestion
        /// </summary>
        private async Task StartTcpListenerAsync(CancellationToken cancellationToken)
        {
            try
            {
                _tcpListener = new TcpListener(IPAddress.Any, _settings.TcpPort);
                _tcpListener.Start();
                _logger.LogInformation($"TCP listener started on port {_settings.TcpPort}");
                while (!cancellationToken.IsCancellationRequested)
                {
                    var tcpClient = await _tcpListener.AcceptTcpClientAsync();
                    _logger.LogInformation($"TCP client connected from {tcpClient.Client.RemoteEndPoint}");
                    // Handle each client connection asynchronously
                    _ = Task.Run(async () => await HandleTcpClientAsync(tcpClient, cancellationToken));
                }
            }
            catch (ObjectDisposedException)
            {
                // Expected when stopping
                _logger.LogInformation("TCP listener stopped");
            }
            catch (Exception ex)
            {
                _logger.LogError("TCP Listener error", ex);
            }
        }

        /// <summary>
        /// Handles individual TCP client connections
        /// </summary>
        private async Task HandleTcpClientAsync(TcpClient client, CancellationToken cancellationToken)
        {
            try
            {
                using (client)
                using (var stream = client.GetStream())
                {
                    var buffer = new byte[_settings.TcpBufferSize];
                    var messageBuffer = new StringBuilder();
                    while (!cancellationToken.IsCancellationRequested && client.Connected)
                    {
                        var bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length, cancellationToken);
                        if (bytesRead == 0) break;
                        var data = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                        messageBuffer.Append(data);
                        // Process complete messages (assuming newline-delimited)
                        string messages = messageBuffer.ToString();
                        var lines = messages.Split('\n');
                        // Keep the last incomplete line in buffer
                        messageBuffer.Clear();
                        if (!messages.EndsWith('\n') && lines.Length > 0)
                        {
                            messageBuffer.Append(lines[lines.Length - 1]);
                            Array.Resize(ref lines, lines.Length - 1);
                        }
                        // Process complete messages
                        foreach (var line in lines)
                        {
                            if (!string.IsNullOrWhiteSpace(line))
                            {
                                await ProcessIncomingMessage(line.Trim());
                            }
                        }
                    }
                }
                _logger.LogInformation("TCP client disconnected");
            }
            catch (Exception ex)
            {
                _logger.LogError("TCP Client handler error", ex);
            }
        }

        /// <summary>
        /// Named Pipe listener for local inter-process communication
        /// </summary>
        private async Task StartNamedPipeListenerAsync(CancellationToken cancellationToken)
        {
            try
            {
                _logger.LogInformation($"Starting Named Pipe listener: {_settings.NamedPipeName}");
                while (!cancellationToken.IsCancellationRequested)
                {
                    _pipeServer = new NamedPipeServerStream(
                        _settings.NamedPipeName,
                        PipeDirection.In,
                        4, // Max instances
                        PipeTransmissionMode.Message,
                        PipeOptions.Asynchronous);
                    await _pipeServer.WaitForConnectionAsync(cancellationToken);
                    _logger.LogInformation("Named Pipe client connected");
                    // Handle pipe client
                    _ = Task.Run(async () => await HandlePipeClientAsync(_pipeServer, cancellationToken));
                }
            }
            catch (OperationCanceledException)
            {
                // Expected when stopping
                _logger.LogInformation("Named Pipe listener stopped");
            }
            catch (Exception ex)
            {
                _logger.LogError("Named Pipe Listener error", ex);
            }
        }

        /// <summary>
        /// Handles Named Pipe client connections
        /// </summary>
        private async Task HandlePipeClientAsync(NamedPipeServerStream pipeServer, CancellationToken cancellationToken)
        {
            try
            {
                using (var reader = new StreamReader(pipeServer))
                {
                    while (!cancellationToken.IsCancellationRequested && pipeServer.IsConnected)
                    {
                        var message = await reader.ReadLineAsync();
                        if (message == null) break;
                        await ProcessIncomingMessage(message);
                    }
                }
                _logger.LogInformation("Named Pipe client disconnected");
            }
            catch (Exception ex)
            {
                _logger.LogError("Named Pipe Client handler error", ex);
            }
        }

        /// <summary>
        /// Processes incoming message and adds to channel
        /// </summary>
        private async Task ProcessIncomingMessage(string messageText)
        {
            try
            {
                var message = ParseMessage(messageText);
                if (message != null)
                {
                    message.ReceiveTimestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                    await _channelWriter.WriteAsync(message);
                    Interlocked.Increment(ref _messagesReceived);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Message processing error for message: {messageText?.Substring(0, Math.Min(100, messageText?.Length ?? 0))}", ex);
            }
        }

        /// <summary>
        /// Parses incoming message text to DataMessage object
        /// </summary>
        private DataMessage ParseMessage(string messageText)
        {
            try
            {
                // Try JSON format first
                if (messageText.StartsWith('{'))
                {
                    return JsonSerializer.Deserialize<DataMessage>(messageText);
                }
                // Parse CSV format: Type,Operation,SendTimestamp,Data
                var parts = messageText.Split(',', 4);
                if (parts.Length < 4) return null;
                return new DataMessage
                {
                    Type = Enum.TryParse<MessageType>(parts[0], out var type) ? type : MessageType.OrderBook,
                    Operation = Enum.TryParse<DataOperation>(parts[1], out var op) ? op : DataOperation.Insert,
                    SendTimestamp = long.TryParse(parts[2], out var ts) ? ts : DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                    Data = parts[3]
                };
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error parsing message: {messageText?.Substring(0, Math.Min(50, messageText?.Length ?? 0))}", ex);
                return null;
            }
        }

        /// <summary>
        /// Main message processing loop with batching
        /// </summary>
        private async Task StartMessageProcessingAsync(CancellationToken cancellationToken)
        {
            var batch = new List<DataMessage>(_settings.BatchSize);
            try
            {
                _logger.LogInformation("Message processing loop started");
                await foreach (var message in _channelReader.ReadAllAsync(cancellationToken))
                {
                    message.QueueTimestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                    batch.Add(message);
                    // Process batch when full or timeout reached
                    var timeSinceLastBatch = DateTime.UtcNow - _lastBatchTime;
                    if (batch.Count >= _settings.BatchSize ||
                        timeSinceLastBatch.TotalMilliseconds >= _settings.BatchTimeoutMs)
                    {
                        await ProcessBatchAsync(batch);
                        batch.Clear();
                        _lastBatchTime = DateTime.UtcNow;
                    }
                }
                // Process any remaining messages
                if (batch.Count > 0)
                {
                    await ProcessBatchAsync(batch);
                }
                _logger.LogInformation("Message processing loop completed");
            }
            catch (OperationCanceledException)
            {
                // Expected when stopping
                _logger.LogInformation("Message processing loop cancelled");
            }
            catch (Exception ex)
            {
                _logger.LogError("Message processing loop error", ex);
            }
        }

        /// <summary>
        /// Processes a batch of messages efficiently
        /// </summary>
        private async Task ProcessBatchAsync(List<DataMessage> batch)
        {
            if (batch.Count == 0) return;

            var orderBookInserts = 0;
            var orderBookUpdates = 0;
            var orderBookDeletes = 0;
            var tradeBookInserts = 0;
            var tradeBookUpdates = 0;
            var tradeBookDeletes = 0;

            var orderBookItems = new List<OrderBookEntry>();
            var tradeBookItems = new List<TradeBookEntry>();

            foreach (var message in batch)
            {
                try
                {
                    message.ApplyTimestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                    switch (message.Type)
                    {
                        case MessageType.OrderBook:
                            if (message.Operation == DataOperation.Delete)
                            {
                                if (_orderBookStore.Delete(message.Data)) orderBookDeletes++;
                            }
                            else
                            {
                                var orderEntry = OrderBookEntry.FromCsv(message.Data);
                                if (orderEntry != null)
                                {
                                    orderBookItems.Add(orderEntry);
                                    if (message.Operation == DataOperation.Insert) orderBookInserts++;
                                    else orderBookUpdates++;
                                }
                            }
                            break;
                        case MessageType.TradeBook:
                            if (message.Operation == DataOperation.Delete)
                            {
                                if (_tradeBookStore.Delete(message.Data)) tradeBookDeletes++;
                            }
                            else
                            {
                                var tradeEntry = TradeBookEntry.FromCsv(message.Data);
                                if (tradeEntry != null)
                                {
                                    tradeBookItems.Add(tradeEntry);
                                    if (message.Operation == DataOperation.Insert) tradeBookInserts++;
                                    else tradeBookUpdates++;
                                }
                            }
                            break;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError($"Error processing message of type {message.Type}: {message.Data?.Substring(0, Math.Min(50, message.Data?.Length ?? 0))}", ex);
                }
            }

            try
            {
                if (orderBookItems.Count > 0)
                    _orderBookStore.BatchInsertOrUpdate(orderBookItems);
                if (tradeBookItems.Count > 0)
                    _tradeBookStore.BatchInsertOrUpdate(tradeBookItems);
            }
            catch (Exception ex)
            {
                _logger.LogError("Error during batch data store update", ex);
                return;
            }

            try
            {
                foreach (var message in batch.Where(m => m.Operation != DataOperation.Delete))
                {
                    await _metricsLogger.LogMessageMetricsAsync(message);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError("Error logging message metrics", ex);
            }

            Interlocked.Add(ref _messagesProcessed, batch.Count);

            try
            {
                DataBatchProcessed?.Invoke(this, new DataBatchProcessedEventArgs
                {
                    OrderBookInserts = orderBookInserts,
                    OrderBookUpdates = orderBookUpdates,
                    OrderBookDeletes = orderBookDeletes,
                    TradeBookInserts = tradeBookInserts,
                    TradeBookUpdates = tradeBookUpdates,
                    TradeBookDeletes = tradeBookDeletes,
                    TotalMessages = batch.Count,
                    BatchProcessingTimeMs = batch.Count > 0 ? batch[^1].ApplyTimestamp - batch[0].QueueTimestamp : 0
                });
            }
            catch (Exception ex)
            {
                _logger.LogError("Error notifying UI of batch completion", ex);
            }
        }

        public void Dispose()
        {
            try
            {
                StopAsync().ConfigureAwait(false).GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                _logger.LogError("Error during IngestWorker disposal", ex);
            }
            finally
            {
                _cancellationSource?.Dispose();
                _tcpListener?.Stop();
                _pipeServer?.Dispose();
            }
        }

        /// <summary>
        /// Event arguments for data batch processing completion
        /// </summary>
        public class DataBatchProcessedEventArgs : EventArgs
        {
            public int OrderBookInserts { get; set; }
            public int OrderBookUpdates { get; set; }
            public int OrderBookDeletes { get; set; }
            public int TradeBookInserts { get; set; }
            public int TradeBookUpdates { get; set; }
            public int TradeBookDeletes { get; set; }
            public int TotalMessages { get; set; }
            public long BatchProcessingTimeMs { get; set; }
        }

        /// <summary>
        /// Statistics for monitoring ingestion performance
        /// </summary>
        public class IngestStats
        {
            public int MessagesReceived { get; set; }
            public int MessagesProcessed { get; set; }
            public int QueueDepth { get; set; }
            public bool IsRunning { get; set; }
            public int OrderBookRows { get; set; }
            public int TradeBookRows { get; set; }
            public double ProcessingRate => MessagesReceived > 0 ? (double)MessagesProcessed / MessagesReceived : 0;
            public string GetFormattedStats()
            {
                return $"Received: {MessagesReceived:N0}, Processed: {MessagesProcessed:N0}, " +
                       $"Queue: {QueueDepth:N0}, Rate: {ProcessingRate:P1}, " +
                       $"Orders: {OrderBookRows:N0}, Trades: {TradeBookRows:N0}";
            }
        }
    }
}
