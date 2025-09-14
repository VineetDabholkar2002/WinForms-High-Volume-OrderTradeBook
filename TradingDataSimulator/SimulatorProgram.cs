//
// SimulatorProgram.cs - Console application for the data simulator
//

using System;
using System.Threading;
using System.Threading.Tasks;

namespace TradingDataSimulator
{
    /// <summary>
    /// Console application for running the trading data simulator
    /// </summary>
    class SimulatorProgram
    {
        private static DataSimulator _simulator;
        private static Timer _statsTimer;
        
        static async Task Main(string[] args)
        {
            Console.WriteLine("=== Trading Data Simulator ===");
            Console.WriteLine("High-performance data generator for testing");
            Console.WriteLine();
            
            try
            {
                _simulator = new DataSimulator();
                
                // Parse command line arguments
                ParseArguments(args);
                
                // Display configuration
                DisplayConfiguration();
                
                // Setup statistics display
                _statsTimer = new Timer(DisplayStats, null, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(5));
                
                // Handle Ctrl+C for graceful shutdown
                Console.CancelKeyPress += async (sender, e) =>
                {
                    e.Cancel = true;
                    Console.WriteLine("\nShutdown requested...");
                    await _simulator.StopAsync();
                    Environment.Exit(0);
                };
                
                Console.WriteLine("Press Ctrl+C to stop the simulator");
                Console.WriteLine("Starting simulation...");
                Console.WriteLine();
                
                // Start the simulator
                await _simulator.StartAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
                Console.WriteLine("\nUsage: TradingDataSimulator.exe [options]");
                Console.WriteLine("Options:");
                Console.WriteLine("  --rate <number>     Messages per second (default: 1000)");
                Console.WriteLine("  --host <hostname>   TCP host (default: localhost)");
                Console.WriteLine("  --port <port>       TCP port (default: 9999)");
                Console.WriteLine("  --pipe <name>       Named pipe name (default: TradingDataPipe)");
                Console.WriteLine("  --tcp               Use TCP transport (default: true)");
                Console.WriteLine("  --pipe-only         Use only Named Pipe transport");
                Console.WriteLine("  --order-ratio <0-1> Ratio of OrderBook messages (default: 0.7)");
                Console.WriteLine();
                Console.WriteLine("Examples:");
                Console.WriteLine("  TradingDataSimulator.exe --rate 5000 --port 8888");
                Console.WriteLine("  TradingDataSimulator.exe --pipe-only --pipe MyPipe --rate 2000");
                
                Environment.Exit(1);
            }
            finally
            {
                _statsTimer?.Dispose();
                _simulator?.Dispose();
            }
        }
        
        /// <summary>
        /// Parses command line arguments and configures the simulator
        /// </summary>
        private static void ParseArguments(string[] args)
        {
            for (int i = 0; i < args.Length; i++)
            {
                switch (args[i].ToLower())
                {
                    case "--rate":
                        if (i + 1 < args.Length && int.TryParse(args[i + 1], out var rate))
                        {
                            _simulator.MessagesPerSecond = Math.Max(1, Math.Min(50000, rate));
                            i++;
                        }
                        break;
                        
                    case "--host":
                        if (i + 1 < args.Length)
                        {
                            _simulator.TcpHost = args[i + 1];
                            i++;
                        }
                        break;
                        
                    case "--port":
                        if (i + 1 < args.Length && int.TryParse(args[i + 1], out var port))
                        {
                            _simulator.TcpPort = Math.Max(1, Math.Min(65535, port));
                            i++;
                        }
                        break;
                        
                    case "--pipe":
                        if (i + 1 < args.Length)
                        {
                            _simulator.NamedPipeName = args[i + 1];
                            _simulator.UseNamedPipe = true;
                            i++;
                        }
                        break;
                        
                    case "--tcp":
                        _simulator.UseTcp = true;
                        _simulator.UseNamedPipe = false;
                        break;
                        
                    case "--pipe-only":
                        _simulator.UseTcp = false;
                        _simulator.UseNamedPipe = true;
                        break;
                        
                    case "--order-ratio":
                        if (i + 1 < args.Length && double.TryParse(args[i + 1], out var ratio))
                        {
                            _simulator.OrderBookRatio = Math.Max(0, Math.Min(1, ratio));
                            i++;
                        }
                        break;
                        
                    case "--help":
                    case "-h":
                    case "/?":
                        throw new ArgumentException("Help requested");
                }
            }
        }
        
        /// <summary>
        /// Displays the current simulator configuration
        /// </summary>
        private static void DisplayConfiguration()
        {
            Console.WriteLine("Configuration:");
            Console.WriteLine($"  Target Rate: {_simulator.MessagesPerSecond:N0} messages/second");
            Console.WriteLine($"  TCP: {_simulator.UseTcp} ({_simulator.TcpHost}:{_simulator.TcpPort})");
            Console.WriteLine($"  Named Pipe: {_simulator.UseNamedPipe} ({_simulator.NamedPipeName})");
            Console.WriteLine($"  OrderBook Ratio: {_simulator.OrderBookRatio:P0}");
            Console.WriteLine();
        }
        
        /// <summary>
        /// Displays current statistics
        /// </summary>
        private static void DisplayStats(object state)
        {
            if (_simulator?.IsRunning == true)
            {
                var stats = _simulator.GetStats();
                var timestamp = DateTime.Now.ToString("HH:mm:ss");
                Console.WriteLine($"[{timestamp}] {stats}");
            }
        }
    }
}