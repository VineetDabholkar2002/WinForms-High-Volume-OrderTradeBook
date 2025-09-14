//
// DataSimulator.cs - High-performance data simulator for testing
// Generates realistic trading data and sends via TCP/Named Pipes
//
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipes;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace TradingDataSimulator
{
    /// <summary>
    /// High-performance data simulator for generating realistic trading data
    /// </summary>
    public class DataSimulator : IDisposable
    {
        private readonly Random _random = new Random();
        private readonly string[] _symbols;
        private readonly CancellationTokenSource _cancellationSource;
        private volatile bool _isRunning;
        private volatile int _messagesSent;
        private readonly DateTime _startTime;

        // Configuration
        public int MessagesPerSecond { get; set; } = 100;
        public string TcpHost { get; set; } = "localhost";
        public int TcpPort { get; set; } = 9999;
        public string NamedPipeName { get; set; } = "TradingDataPipe";
        public bool UseTcp { get; set; } = true;
        public bool UseNamedPipe { get; set; } = false;
        public double OrderBookRatio { get; set; } = 0.7; // 70% OrderBook, 30% TradeBook

        // Messages state for deletes/updates
        private readonly HashSet<string> _knownOrderIds = new HashSet<string>();
        private readonly HashSet<string> _knownTradeIds = new HashSet<string>();

        // Sample data
        private readonly string[] _sides = { "Buy", "Sell" };
        private readonly string[] _orderTypes = { "Market", "Limit", "Stop", "StopLimit" };
        private readonly string[] _exchanges = { "NYSE", "NASDAQ", "BATS", "ARCA", "EDGX" };
        private readonly string[] _currencies = { "USD", "EUR", "GBP", "JPY", "CHF" };
        private readonly string[] _strategies = { "Momentum", "Arbitrage", "MarketMaking", "MeanReversion", "Scalping" };

        public long MessagesSent => _messagesSent;
        public bool IsRunning => _isRunning;

        public DataSimulator()
        {
            _symbols = GenerateSymbols(100);
            _cancellationSource = new CancellationTokenSource();
            _startTime = DateTime.UtcNow;
        }

        /// <summary>
        /// Generates a list of realistic trading symbols
        /// </summary>
        private string[] GenerateSymbols(int count)
        {
            var symbols = new List<string>();
            // Add some real symbols
            var realSymbols = new[] { "AAPL", "MSFT", "GOOGL", "AMZN", "TSLA", "META", "NVDA", "AMD", "INTC", "NFLX",
                                     "CRM", "ORCL", "ADBE", "PYPL", "UBER", "LYFT", "SPOT", "SQ", "ROKU", "ZM" };
            symbols.AddRange(realSymbols);
            // Generate additional symbols
            for (int i = symbols.Count; i < count; i++)
            {
                var symbolLength = _random.Next(3, 6);
                var symbol = "";
                for (int j = 0; j < symbolLength; j++)
                {
                    symbol += (char)('A' + _random.Next(26));
                }
                symbols.Add(symbol);
            }
            return symbols.ToArray();
        }

        /// <summary>
        /// Starts the data simulation
        /// </summary>
        public async Task StartAsync()
        {
            if (_isRunning) return;
            _isRunning = true;
            _messagesSent = 0;
            Console.WriteLine($"Starting data simulator...");
            Console.WriteLine($"Target Rate: {MessagesPerSecond:N0} messages/second");
            Console.WriteLine($"TCP: {UseTcp} ({TcpHost}:{TcpPort})");
            Console.WriteLine($"Named Pipe: {UseNamedPipe} ({NamedPipeName})");
            Console.WriteLine($"OrderBook Ratio: {OrderBookRatio:P0}");
            var tasks = new List<Task>();
            if (UseTcp)
                tasks.Add(RunTcpSimulationAsync(_cancellationSource.Token));
            if (UseNamedPipe)
                tasks.Add(RunNamedPipeSimulationAsync(_cancellationSource.Token));
            if (tasks.Count == 0)
                throw new InvalidOperationException("No transport method selected");
            await Task.WhenAll(tasks);
        }

        /// <summary>
        /// Stops the data simulation
        /// </summary>
        public async Task StopAsync()
        {
            if (!_isRunning) return;
            _isRunning = false;
            _cancellationSource.Cancel();
            Console.WriteLine($"Stopping simulator. Total messages sent: {_messagesSent:N0}");
        }

        /// <summary>
        /// Runs TCP-based data simulation with batching for high throughput
        /// </summary>
        private async Task RunTcpSimulationAsync(CancellationToken cancellationToken)
        {
            try
            {
                using var tcpClient = new TcpClient();
                await tcpClient.ConnectAsync(TcpHost, TcpPort);
                using var stream = tcpClient.GetStream();
                Console.WriteLine($"Connected to TCP server at {TcpHost}:{TcpPort}");

                var batchSize = Math.Max(1, MessagesPerSecond / 10); // ~10ms worth
                var interval = TimeSpan.FromMilliseconds(1000.0 * batchSize / MessagesPerSecond);

                var buffer = new List<byte>(batchSize * 512); // pre-allocate ~512B/message
                var stopwatch = new System.Diagnostics.Stopwatch();
                stopwatch.Start();

                while (!cancellationToken.IsCancellationRequested && tcpClient.Connected)
                {
                    buffer.Clear();

                    // generate batch
                    for (int i = 0; i < batchSize; i++)
                    {
                        var message = GenerateMessage() + "\n";
                        var msgBytes = Encoding.UTF8.GetBytes(message);
                        buffer.AddRange(msgBytes);
                    }

                    // write batch
                    await stream.WriteAsync(buffer.ToArray(), 0, buffer.Count, cancellationToken);
                    await stream.FlushAsync(cancellationToken);
                    Interlocked.Add(ref _messagesSent, batchSize);

                    // pacing
                    var elapsed = stopwatch.Elapsed;
                    if (elapsed < interval)
                    {
                        await Task.Delay(interval - elapsed, cancellationToken);
                    }
                    stopwatch.Restart();
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                Console.WriteLine($"TCP simulation error: {ex.Message}");
            }
        }

        //private async Task RunTcpSimulationAsync(CancellationToken cancellationToken)
        //{
        //    try
        //    {
        //        using var tcpClient = new TcpClient();
        //        await tcpClient.ConnectAsync(TcpHost, TcpPort);
        //        using var stream = tcpClient.GetStream();
        //        Console.WriteLine($"Connected to TCP server at {TcpHost}:{TcpPort}");
        //        var targetIntervalMs = 1000.0 / MessagesPerSecond;
        //        var lastSendTime = DateTime.UtcNow;
        //        while (!cancellationToken.IsCancellationRequested && tcpClient.Connected)
        //        {
        //            var now = DateTime.UtcNow;
        //            var elapsed = (now - lastSendTime).TotalMilliseconds;
        //            if (elapsed >= targetIntervalMs)
        //            {
        //                var message = GenerateMessage();
        //                var messageBytes = Encoding.UTF8.GetBytes(message + "\n");
        //                await stream.WriteAsync(messageBytes, 0, messageBytes.Length, cancellationToken);
        //                await stream.FlushAsync(cancellationToken);
        //                Interlocked.Increment(ref _messagesSent);
        //                lastSendTime = now;
        //            }
        //            else
        //            {
        //                var sleepTime = Math.Max(1, (int)(targetIntervalMs - elapsed));
        //                await Task.Delay(sleepTime, cancellationToken);
        //            }
        //        }
        //    }
        //    catch (OperationCanceledException) { }
        //    catch (Exception ex)
        //    {
        //        Console.WriteLine($"TCP simulation error: {ex.Message}");
        //    }
        //}

        /// <summary>
        /// Runs Named Pipe–based data simulation with batching for high throughput
        /// </summary>
        private async Task RunNamedPipeSimulationAsync(CancellationToken cancellationToken)
        {
            try
            {
                using var pipeClient = new NamedPipeClientStream(".", NamedPipeName, PipeDirection.Out, PipeOptions.Asynchronous);
                await pipeClient.ConnectAsync(cancellationToken);
                Console.WriteLine($"Connected to Named Pipe server at {NamedPipeName}");

                var batchSize = Math.Max(1, MessagesPerSecond / 100); // ~10ms worth
                var interval = TimeSpan.FromMilliseconds(1000.0 * batchSize / MessagesPerSecond);

                var buffer = new List<byte>(batchSize * 512); // pre-allocate ~512B/message
                var stopwatch = new System.Diagnostics.Stopwatch();
                stopwatch.Start();

                while (!cancellationToken.IsCancellationRequested && pipeClient.IsConnected)
                {
                    buffer.Clear();

                    // generate batch
                    for (int i = 0; i < batchSize; i++)
                    {
                        var message = GenerateMessage() + "\n";
                        var msgBytes = Encoding.UTF8.GetBytes(message);
                        buffer.AddRange(msgBytes);
                    }

                    // write batch
                    await pipeClient.WriteAsync(buffer.ToArray(), 0, buffer.Count, cancellationToken);
                    await pipeClient.FlushAsync(cancellationToken);
                    Interlocked.Add(ref _messagesSent, batchSize);

                    // pacing
                    var elapsed = stopwatch.Elapsed;
                    if (elapsed < interval)
                    {
                        await Task.Delay(interval - elapsed, cancellationToken);
                    }
                    stopwatch.Restart();
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                Console.WriteLine($"Named Pipe simulation error: {ex.Message}");
            }
        }


        /// <summary>
        /// Generates a realistic trading message with Insert, Update, or Delete
        /// </summary>
        private string GenerateMessage()
        {
            var sendTimestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

            bool isOrder = _random.NextDouble() < OrderBookRatio;

            // Select operation with probabilities
            double opChance = _random.NextDouble();
            string operation;
            string data;

            if (isOrder)
            {
                if (opChance < 0.05 && _knownOrderIds.Count > 0) // 5% deletes
                {
                    operation = "Delete";
                    data = PickRandomKey(_knownOrderIds);
                }
                else if (opChance < 0.25 && _knownOrderIds.Count > 0) // 20% updates
                {
                    operation = "Update";
                    data = GenerateOrderBookCsv(updateExisting: true);
                }
                else
                {
                    operation = "Insert";
                    data = GenerateOrderBookCsv();
                }
                return $"OrderBook,{operation},{sendTimestamp},{data}";
            }
            else
            {
                if (opChance < 0.05 && _knownTradeIds.Count > 0) // 5% deletes
                {
                    operation = "Delete";
                    data = PickRandomKey(_knownTradeIds);
                }
                else if (opChance < 0.25 && _knownTradeIds.Count > 0) // 20% updates
                {
                    operation = "Update";
                    data = GenerateTradeBookCsv(updateExisting: true);
                }
                else
                {
                    operation = "Insert";
                    data = GenerateTradeBookCsv();
                }
                return $"TradeBook,{operation},{sendTimestamp},{data}";
            }
        }

        private string PickRandomKey(HashSet<string> keySet)
        {
            int index = _random.Next(keySet.Count);
            var enumerator = keySet.GetEnumerator();
            for (int i = 0; i <= index; i++)
                enumerator.MoveNext();
            return enumerator.Current;
        }

        /// <summary>
        /// Generates realistic OrderBook CSV data (50 fields)
        /// If updateExisting is true, pick existing ID for update; else generate new ID.
        /// </summary>
        private string GenerateOrderBookCsv(bool updateExisting = false)
        {
            string orderId;
            if (updateExisting && _knownOrderIds.Count > 0)
            {
                orderId = PickRandomKey(_knownOrderIds);
            }
            else
            {
                orderId = $"ORD{_random.Next(1000000, 9999999)}";
                _knownOrderIds.Add(orderId);
            }
            var symbol = _symbols[_random.Next(_symbols.Length)];
            var side = _sides[_random.Next(_sides.Length)];
            var price = Math.Round(50 + _random.NextDouble() * 950, 2);
            var quantity = _random.Next(100, 10000);
            var timestamp = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss.fff");
            var status = "Active";
            var orderType = _orderTypes[_random.Next(_orderTypes.Length)];
            var timeInForce = "DAY";
            var stopPrice = Math.Round(price * (0.95 + _random.NextDouble() * 0.1), 2);
            var limitPrice = Math.Round(price * (0.98 + _random.NextDouble() * 0.04), 2);
            var filledQuantity = _random.Next(0, quantity / 2);
            var remainingQuantity = quantity - filledQuantity;
            var avgFillPrice = filledQuantity > 0 ? Math.Round(price * (0.999 + _random.NextDouble() * 0.002), 2) : 0;
            var exchange = _exchanges[_random.Next(_exchanges.Length)];
            var clientId = $"CLIENT_{_random.Next(1000, 9999)}";
            var accountId = $"ACC_{_random.Next(10000, 99999)}";
            var traderId = $"TRADER_{_random.Next(100, 999)}";
            var strategy = _strategies[_random.Next(_strategies.Length)];
            var portfolio = $"PORT_{_random.Next(10, 99)}";
            var riskLimit = Math.Round(_random.NextDouble() * 1000000, 2);
            var exposureAmount = Math.Round(_random.NextDouble() * 500000, 2);
            var riskGroup = $"RG_{_random.Next(1, 10)}";
            var marginRequirement = Math.Round(price * quantity * 0.1, 2);
            var currency = _currencies[_random.Next(_currencies.Length)];
            var bidPrice = Math.Round(price - _random.NextDouble() * 0.5, 2);
            var askPrice = Math.Round(price + _random.NextDouble() * 0.5, 2);
            var midPrice = Math.Round((bidPrice + askPrice) / 2, 2);
            var spreadBps = Math.Round((askPrice - bidPrice) / midPrice * 10000, 1);
            var bidSize = _random.Next(100, 5000);
            var askSize = _random.Next(100, 5000);
            var lastPrice = Math.Round(price * (0.99 + _random.NextDouble() * 0.02), 2);
            var volume = _random.Next(10000, 1000000);
            var vwap = Math.Round(price * (0.995 + _random.NextDouble() * 0.01), 2);

            var fields = new List<string>
            {
                orderId, symbol, side, price.ToString(), quantity.ToString(), timestamp, status,
                orderType, timeInForce, stopPrice.ToString(), limitPrice.ToString(), filledQuantity.ToString(),
                remainingQuantity.ToString(), avgFillPrice.ToString(), exchange, clientId, accountId,
                traderId, strategy, portfolio, riskLimit.ToString(), exposureAmount.ToString(), riskGroup,
                marginRequirement.ToString(), currency, bidPrice.ToString(), askPrice.ToString(), midPrice.ToString(), spreadBps.ToString(),
                bidSize.ToString(), askSize.ToString(), lastPrice.ToString(), volume.ToString(), vwap.ToString()
            };
            // Fill remaining fields up to 50 with tags/values matching your data model
            for (int i = fields.Count; i < 50; i++)
            {
                if (i < 44)
                    fields.Add($"Tag{i - 33}");
                else if (i < 49)
                    fields.Add(_random.NextDouble().ToString("F2"));
                else
                    fields.Add(_random.Next(1000).ToString());
            }
            return string.Join(",", fields);
        }

        /// <summary>
        /// Generates realistic TradeBook CSV data (50 fields)
        /// If updateExisting is true, pick existing ID for update; else generate new ID.
        /// </summary>
        private string GenerateTradeBookCsv(bool updateExisting = false)
        {
            string tradeId;
            if (updateExisting && _knownTradeIds.Count > 0)
            {
                tradeId = PickRandomKey(_knownTradeIds);
            }
            else
            {
                tradeId = $"TRD{_random.Next(1000000, 9999999)}";
                _knownTradeIds.Add(tradeId);
            }

            var symbol = _symbols[_random.Next(_symbols.Length)];
            var side = _sides[_random.Next(_sides.Length)];
            var price = Math.Round(50 + _random.NextDouble() * 950, 2);
            var quantity = _random.Next(100, 5000);
            var timestamp = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss.fff");
            var status = "Executed";
            var buyOrderId = $"ORD{_random.Next(1000000, 9999999)}";
            var sellOrderId = $"ORD{_random.Next(1000000, 9999999)}";
            var commission = Math.Round(price * quantity * 0.001, 2);
            var fees = Math.Round(commission * 0.1, 2);
            var netAmount = Math.Round(price * quantity - commission - fees, 2);
            var settlementDate = DateTime.UtcNow.AddDays(2).ToString("yyyy-MM-dd");
            var clearingFirm = $"CLEAR_{_random.Next(10, 99)}";
            var exchange = _exchanges[_random.Next(_exchanges.Length)];
            var buyerId = $"BUYER_{_random.Next(1000, 9999)}";
            var sellerId = $"SELLER_{_random.Next(1000, 9999)}";
            var buyerAccount = $"BACC_{_random.Next(10000, 99999)}";
            var sellerAccount = $"SACC_{_random.Next(10000, 99999)}";
            var executingBroker = $"BROKER_{_random.Next(100, 999)}";
            var riskGroup = $"RG_{_random.Next(1, 10)}";
            var exposureImpact = Math.Round(_random.NextDouble() * 100000, 2);
            var complianceStatus = "Cleared";
            var regReportingStatus = "Reported";
            var currency = _currencies[_random.Next(_currencies.Length)];
            var marketPrice = Math.Round(price * (0.999 + _random.NextDouble() * 0.002), 2);
            var priceDeviation = Math.Round(Math.Abs(price - marketPrice), 4);
            var marketImpact = Math.Round(_random.NextDouble() * 0.01, 4);
            var marketVolume = _random.Next(100000, 10000000);
            var vwap = Math.Round(price * (0.998 + _random.NextDouble() * 0.004), 2);
            var twapPrice = Math.Round(price * (0.999 + _random.NextDouble() * 0.002), 2);
            var tradeCondition = "Normal";

            var fields = new List<string>
            {
                tradeId, symbol, side, price.ToString(), quantity.ToString(), timestamp, status,
                buyOrderId, sellOrderId, commission.ToString(), fees.ToString(), netAmount.ToString(), settlementDate,
                clearingFirm, exchange, buyerId, sellerId, buyerAccount, sellerAccount,
                executingBroker, riskGroup, exposureImpact.ToString(), complianceStatus,
                regReportingStatus, currency, marketPrice.ToString(), priceDeviation.ToString(), marketImpact.ToString(),
                marketVolume.ToString(), vwap.ToString(), twapPrice.ToString(), tradeCondition
            };
            // Fill fields to 42 columns with Tags
            for (int i = fields.Count; i < 42; i++)
            {
                fields.Add($"Tag{i - 31}");
            }
            // Fill next 5 columns with decimal values
            for (int i = 42; i < 47; i++)
            {
                fields.Add(_random.NextDouble().ToString("F2"));
            }
            // Explicitly add the last 3 counters to reach 50 columns
            fields.Add(_random.Next(1000).ToString()); // Counter1 (index 47)
            fields.Add(_random.Next(1000).ToString()); // Counter2 (index 48)
            fields.Add(_random.Next(1000).ToString()); // Counter3 (index 49)
            return string.Join(",", fields);
        }

        /// <summary>
        /// Gets simulation statistics
        /// </summary>
        public string GetStats()
        {
            var elapsedSeconds = (DateTime.UtcNow - _startTime).TotalSeconds;
            var rate = (_messagesSent > 0 && elapsedSeconds > 0) ? _messagesSent / elapsedSeconds : 0;
            return $"Messages sent: {_messagesSent:N0}, Rate: {rate:F1}/sec, Running: {_isRunning}";
        }

        public void Dispose()
        {
            StopAsync().ConfigureAwait(false).GetAwaiter().GetResult();
            _cancellationSource?.Dispose();
        }
    }
}
