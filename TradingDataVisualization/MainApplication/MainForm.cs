//
// MainForm.cs - Modern dark-themed WinForms UI with high-performance DataGridViews
// Supports real-time data visualization with search and filtering capabilities
//
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using TradingDataVisualization.DataLayer;
using TradingDataVisualization.Workers;
using TradingDataVisualization.Infrastructure;
using DataBatchProcessedEventArgs = TradingDataVisualization.Workers.IngestWorker.DataBatchProcessedEventArgs;
using TradingDataVisualization.Logs;

namespace TradingDataVisualization
{
    /// <summary>
    /// Main application form implementing modern dark-themed UI with high-performance virtual mode DataGridViews.
    /// </summary>
    public partial class MainForm : Form
    {
        #region Private Fields
        // Core data storage components
        private readonly ColumnOrientedDataStore<OrderBookEntry> _orderBookStore;
        private readonly ColumnOrientedDataStore<TradeBookEntry> _tradeBookStore;
        // Background processing and monitoring
        private readonly IngestWorker _ingestWorker;
        private readonly MetricsLogger _metricsLogger;
        private readonly AppSettings _settings;
        // Dependency injection
        private readonly ILogger _logger;
        // UI Controls - Data Grids
        private DataGridView _orderBookGrid;
        private DataGridView _tradeBookGrid;
        // UI Controls - Search Components
        private TextBox _orderSearchBox;
        private TextBox _tradeSearchBox;
        private Label _orderStatsLabel;
        private Label _tradeStatsLabel;
        // UI Controls - Status and Information
        private StatusStrip _statusStrip;
        private ToolStripStatusLabel _statusLabel;
        private ToolStripStatusLabel _metricsLabel;
        private System.Windows.Forms.Timer _uiUpdateTimer;
        // Search and filtering state management
        private List<int> _filteredOrderRows = new List<int>();
        private List<int> _filteredTradeRows = new List<int>();
        private bool _orderSearchActive = false;
        private bool _tradeSearchActive = false;
        // Alive rows lists for no-search mode filtering deleted rows
        private List<int> _allAliveOrderRows = new List<int>();
        private List<int> _allAliveTradeRows = new List<int>();
        // Performance optimization constants and tracking
        private readonly int UI_UPDATE_INTERVAL_MS = 16; // ~30 FPS for smooth updates 16 -> ~60 FPS (1000ms / 60 ~ 16ms)
        private DateTime _lastOrderGridUpdate = DateTime.MinValue;
        private DateTime _lastTradeGridUpdate = DateTime.MinValue;
        // UI render queue depth tracking for metrics
        private volatile int _uiRenderQueueDepth = 0;
        #endregion

        #region Constructor
        /// <summary>
        /// Initializes a new instance of the MainForm with dependency injection.
        /// </summary>
        public MainForm(ILogger logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            try
            {
                _logger.LogInformation("MainForm initialization started");

                // Load application configuration
                _settings = new AppSettings();
                UI_UPDATE_INTERVAL_MS = _settings.UiUpdateIntervalMs;
                // Initialize high-performance column-oriented data stores
                _orderBookStore = new ColumnOrientedDataStore<OrderBookEntry>(
                    GetOrderBookColumnNames(),
                    order => order.OrderId,
                    order => order.ToObjectArray(),
                    OrderBookEntry.FromCsv);
                _tradeBookStore = new ColumnOrientedDataStore<TradeBookEntry>(
                    GetTradeBookColumnNames(),
                    trade => trade.TradeId,
                    trade => trade.ToObjectArray(),
                    TradeBookEntry.FromCsv);
                // Initialize monitoring and utility components
                _metricsLogger = new MetricsLogger();
                // Set up background data ingestion worker
                _ingestWorker = new IngestWorker(_orderBookStore, _tradeBookStore, _metricsLogger, _settings, _logger);
                _ingestWorker.DataBatchProcessed += OnDataBatchProcessed;
                // Initialize Windows Forms components and UI
                InitializeComponent();
                ApplyDarkTheme();
                SetupDataGrids();
                SetupUI();
                // Start background data ingestion
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await _ingestWorker.StartAsync();
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError("Failed to start ingestion worker", ex);
                    }
                });
                _logger.LogInformation("MainForm initialization completed successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError("Critical error during MainForm initialization", ex);
                throw;
            }
        }
        #endregion

        #region Data Column Definitions
        /// <summary>
        /// Defines the complete set of 50 column names for the Order Book DataGridView.
        /// </summary>
        private string[] GetOrderBookColumnNames()
        {
            return new string[]
            {
                // Core Order Information (7 columns)
                "OrderId", "Symbol", "Side", "Price", "Quantity", "Timestamp", "Status",
                // Order Type and Execution Details (8 columns)
                "OrderType", "TimeInForce", "StopPrice", "LimitPrice", "FilledQuantity",
                "RemainingQuantity", "AvgFillPrice", "Exchange",
                // Trading Entity Information (6 columns)
                "ClientId", "AccountId", "TraderId", "Strategy", "Portfolio", "Currency",
                // Risk Management Fields (4 columns)
                "RiskLimit", "ExposureAmount", "RiskGroup", "MarginRequirement",
                // Market Data Fields (9 columns)
                "BidPrice", "AskPrice", "MidPrice", "SpreadBps", "BidSize",
                "AskSize", "LastPrice", "Volume", "VWAP",
                // Extensible Tag Fields (10 columns)
                "Tag1", "Tag2", "Tag3", "Tag4", "Tag5",
                "Tag6", "Tag7", "Tag8", "Tag9", "Tag10",
                // Value Fields (5 columns)
                "Value1", "Value2", "Value3", "Value4", "Value5",
                // Counter Field (1 column)
                "Counter1"
            };
        }
        /// <summary>
        /// Defines the complete set of 50 column names for the Trade Book DataGridView.
        /// </summary>
        private string[] GetTradeBookColumnNames()
        {
            return new string[]
            {
                // Core Trade Information (7 columns)
                "TradeId", "Symbol", "Side", "Price", "Quantity", "Timestamp", "Status",
                // Order Relationship and Financial Details (6 columns)
                "BuyOrderId", "SellOrderId", "Commission", "Fees", "NetAmount", "SettlementDate",
                // Trading Participants (8 columns)
                "ClearingFirm", "Exchange", "BuyerId", "SellerId", "BuyerAccount",
                "SellerAccount", "ExecutingBroker", "Currency",
                // Risk and Compliance (4 columns)
                "RiskGroup", "ExposureImpact", "ComplianceStatus", "RegReportingStatus",
                // Market Impact and Pricing (7 columns)
                "MarketPrice", "PriceDeviation", "MarketImpact", "MarketVolume",
                "VWAP", "TWAPPrice", "TradeCondition",
                // Extensible Tag Fields (10 columns)
                "Tag1", "Tag2", "Tag3", "Tag4", "Tag5",
                "Tag6", "Tag7", "Tag8", "Tag9", "Tag10",
                // Value Fields (5 columns)
                "Value1", "Value2", "Value3", "Value4", "Value5",
                // Counter Fields (3 columns)
                "Counter1", "Counter2", "Counter3"
            };
        }
        #endregion

        #region UI Theme and Styling
        /// <summary>
        /// Applies a professional dark theme to the main form and sets window properties.
        /// </summary>
        private void ApplyDarkTheme()
        {
            this.BackColor = Color.FromArgb(30, 30, 30);
            this.ForeColor = Color.FromArgb(220, 220, 220);
            this.Text = "High-Performance Trading Data Visualization";
            this.WindowState = FormWindowState.Maximized;
            this.StartPosition = FormStartPosition.CenterScreen;
            this.MinimumSize = new Size(1200, 800);
        }
        #endregion

        #region DataGridView Setup and Configuration
        /// <summary>
        /// Configures high-performance virtual mode DataGridViews for both Order Book and Trade Book.
        /// </summary>
        private void SetupDataGrids()
        {
            // Configure Order Book Grid
            _orderBookGrid = CreateVirtualDataGridView();
            var orderColumns = GetOrderBookColumnNames();
            foreach (var colName in orderColumns)
            {
                _orderBookGrid.Columns.Add(colName, colName);
                _orderBookGrid.Columns[_orderBookGrid.Columns.Count - 1].Width = 100;
            }
            _orderBookGrid.CellValueNeeded += OrderBookGrid_CellValueNeeded;
            // Configure Trade Book Grid
            _tradeBookGrid = CreateVirtualDataGridView();
            var tradeColumns = GetTradeBookColumnNames();
            foreach (var colName in tradeColumns)
            {
                _tradeBookGrid.Columns.Add(colName, colName);
                _tradeBookGrid.Columns[_tradeBookGrid.Columns.Count - 1].Width = 100;
            }
            _tradeBookGrid.CellValueNeeded += TradeBookGrid_CellValueNeeded;
            // Enable performance optimizations
            EnableDoubleBuffering(_orderBookGrid);
            EnableDoubleBuffering(_tradeBookGrid);
        }
        /// <summary>
        /// Creates a standardized virtual mode DataGridView with dark theme styling and optimized settings.
        /// </summary>
        private DataGridView CreateVirtualDataGridView()
        {
            return new DataGridView
            {
                VirtualMode = true,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                ReadOnly = true,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = false,
                RowHeadersVisible = false,
                BackgroundColor = Color.FromArgb(40, 40, 40),
                GridColor = Color.FromArgb(60, 60, 60),
                DefaultCellStyle = new DataGridViewCellStyle
                {
                    BackColor = Color.FromArgb(45, 45, 45),
                    ForeColor = Color.FromArgb(220, 220, 220),
                    SelectionBackColor = Color.FromArgb(70, 130, 180),
                    SelectionForeColor = Color.White,
                    Font = new Font("Consolas", 9F)
                },
                ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle
                {
                    BackColor = Color.FromArgb(50, 50, 50),
                    ForeColor = Color.FromArgb(240, 240, 240),
                    Font = new Font("Segoe UI", 9F, FontStyle.Bold)
                },
                EnableHeadersVisualStyles = false,
                BorderStyle = BorderStyle.None,
                CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None,
                AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.None,
                RowHeadersWidthSizeMode = DataGridViewRowHeadersWidthSizeMode.DisableResizing
            };
        }
        /// <summary>
        /// Enables double buffering on a DataGridView to reduce flicker during updates.
        /// </summary>
        private void EnableDoubleBuffering(DataGridView dgv)
        {
            if (!SystemInformation.TerminalServerSession)
            {
                try
                {
                    var dgvType = dgv.GetType();
                    var pi = dgvType.GetProperty("DoubleBuffered",
                        System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                    pi?.SetValue(dgv, true, null);
                }
                catch (Exception ex)
                {
                    _logger.LogError("Failed to enable double buffering", ex);
                }
            }
        }
        #endregion

        #region UI Layout Setup
        /// <summary>
        /// Constructs the main UI layout with split panels for Order Book and Trade Book grids.
        /// </summary>
        private void SetupUI()
        {
            this.SuspendLayout();
            try
            {
                // Main layout container - splits form vertically 50/50
                var tableLayout = new TableLayoutPanel
                {
                    Dock = DockStyle.Fill,
                    RowCount = 2,
                    ColumnCount = 1,
                    BackColor = Color.FromArgb(30, 30, 30)
                };
                tableLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 50F));
                tableLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 50F));
                #region Order Book Panel Setup
                var orderPanel = new Panel { Dock = DockStyle.Fill };
                var orderSearchPanel = new FlowLayoutPanel
                {
                    Height = 40,
                    Dock = DockStyle.Top,
                    BackColor = Color.FromArgb(35, 35, 35),
                    Padding = new Padding(10, 7, 10, 7),
                    FlowDirection = FlowDirection.LeftToRight,
                    WrapContents = false,
                    AutoSize = true,
                    AutoSizeMode = AutoSizeMode.GrowAndShrink
                };
                var orderSearchLabel = new Label
                {
                    Text = "Order Book Search (ID/Symbol):",
                    ForeColor = Color.FromArgb(220, 220, 220),
                    AutoSize = true,
                    TextAlign = ContentAlignment.MiddleLeft,
                    Padding = new Padding(0, 6, 0, 0)
                };
                _orderSearchBox = new TextBox
                {
                    Width = 300,
                    BackColor = Color.FromArgb(50, 50, 50),
                    ForeColor = Color.FromArgb(220, 220, 220),
                    BorderStyle = BorderStyle.FixedSingle
                };
                _orderSearchBox.TextChanged += OrderSearchBox_TextChanged;
                _orderStatsLabel = new Label
                {
                    Text = "Rows: 0",
                    ForeColor = Color.FromArgb(180, 180, 180),
                    AutoSize = true,
                    TextAlign = ContentAlignment.MiddleLeft,
                    Padding = new Padding(20, 6, 0, 0)
                };
                orderSearchPanel.Controls.Add(orderSearchLabel);
                orderSearchPanel.Controls.Add(_orderSearchBox);
                orderSearchPanel.Controls.Add(_orderStatsLabel);
                orderPanel.Controls.Add(_orderBookGrid);
                orderPanel.Controls.Add(orderSearchPanel);
                _orderBookGrid.Dock = DockStyle.Fill;
                #endregion
                #region Trade Book Panel Setup
                var tradePanel = new Panel { Dock = DockStyle.Fill };
                var tradeSearchPanel = new FlowLayoutPanel
                {
                    Height = 40,
                    Dock = DockStyle.Top,
                    BackColor = Color.FromArgb(35, 35, 35),
                    Padding = new Padding(10, 7, 10, 7),
                    FlowDirection = FlowDirection.LeftToRight,
                    WrapContents = false,
                    AutoSize = true,
                    AutoSizeMode = AutoSizeMode.GrowAndShrink
                };
                var tradeSearchLabel = new Label
                {
                    Text = "Trade Book Search (ID/Symbol):",
                    ForeColor = Color.FromArgb(220, 220, 220),
                    AutoSize = true,
                    TextAlign = ContentAlignment.MiddleLeft,
                    Padding = new Padding(0, 6, 0, 0)
                };
                _tradeSearchBox = new TextBox
                {
                    Width = 300,
                    BackColor = Color.FromArgb(50, 50, 50),
                    ForeColor = Color.FromArgb(220, 220, 220),
                    BorderStyle = BorderStyle.FixedSingle
                };
                _tradeSearchBox.TextChanged += TradeSearchBox_TextChanged;
                _tradeStatsLabel = new Label
                {
                    Text = "Rows: 0",
                    ForeColor = Color.FromArgb(180, 180, 180),
                    AutoSize = true,
                    TextAlign = ContentAlignment.MiddleLeft,
                    Padding = new Padding(20, 6, 0, 0)
                };
                tradeSearchPanel.Controls.Add(tradeSearchLabel);
                tradeSearchPanel.Controls.Add(_tradeSearchBox);
                tradeSearchPanel.Controls.Add(_tradeStatsLabel);
                tradePanel.Controls.Add(_tradeBookGrid);
                tradePanel.Controls.Add(tradeSearchPanel);
                _tradeBookGrid.Dock = DockStyle.Fill;
                #endregion
                #region Layout Assembly
                tableLayout.Controls.Add(orderPanel, 0, 0);
                tableLayout.Controls.Add(tradePanel, 0, 1);
                #endregion
                #region Status Strip Setup
                _statusStrip = new StatusStrip
                {
                    BackColor = Color.FromArgb(25, 25, 25),
                    ForeColor = Color.FromArgb(220, 220, 220)
                };
                _statusLabel = new ToolStripStatusLabel("Ready")
                {
                    ForeColor = Color.FromArgb(220, 220, 220)
                };
                _metricsLabel = new ToolStripStatusLabel("Metrics: Initializing...")
                {
                    ForeColor = Color.FromArgb(180, 180, 180),
                    Spring = true,
                    TextAlign = ContentAlignment.MiddleRight
                };
                _statusStrip.Items.AddRange(new ToolStripItem[] { _statusLabel, _metricsLabel });
                #endregion
                #region Form Assembly and Timer Setup
                this.Controls.Add(tableLayout);
                this.Controls.Add(_statusStrip);
                _uiUpdateTimer = new System.Windows.Forms.Timer
                {
                    Interval = UI_UPDATE_INTERVAL_MS,
                    Enabled = true
                };
                _uiUpdateTimer.Tick += UiUpdateTimer_Tick;
                #endregion
            }
            catch (Exception ex)
            {
                _logger.LogError("Error during UI setup", ex);
                throw;
            }
            finally
            {
                this.ResumeLayout(false);
            }
        }
        #endregion

        #region Virtual Mode Event Handlers
        /// <summary>
        /// Handles virtual mode cell value requests for the Order Book DataGridView.
        /// </summary>
        private void OrderBookGrid_CellValueNeeded(object sender, DataGridViewCellValueEventArgs e)
        {
            try
            {
                if (_orderSearchActive)
                {
                    if (_filteredOrderRows.Count == 0)
                    {
                        // No matches: no value to show
                        e.Value = null;
                        return;
                    }
                    if (e.RowIndex >= _filteredOrderRows.Count)
                    {
                        e.Value = null;
                        return;
                    }
                    int actualRowIndex = _filteredOrderRows[e.RowIndex];
                    e.Value = _orderBookStore.GetCellValue(actualRowIndex, e.ColumnIndex);
                }
                else
                {
                    // Use alive rows list to skip deleted rows
                    if (_allAliveOrderRows.Count == 0 || e.RowIndex >= _allAliveOrderRows.Count)
                    {
                        e.Value = null;
                        return;
                    }
                    int actualRowIndex = _allAliveOrderRows[e.RowIndex];
                    e.Value = _orderBookStore.GetCellValue(actualRowIndex, e.ColumnIndex);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error retrieving Order Book cell value at ({e.RowIndex}, {e.ColumnIndex})", ex);
                e.Value = null;
            }
        }
        /// <summary>
        /// Handles virtual mode cell value requests for the Trade Book DataGridView.
        /// </summary>
        private void TradeBookGrid_CellValueNeeded(object sender, DataGridViewCellValueEventArgs e)
        {
            try
            {
                if (_tradeSearchActive)
                {
                    if (_filteredTradeRows.Count == 0)
                    {
                        // No matches: no value to show
                        e.Value = null;
                        return;
                    }
                    if (e.RowIndex >= _filteredTradeRows.Count)
                    {
                        e.Value = null;
                        return;
                    }
                    int actualRowIndex = _filteredTradeRows[e.RowIndex];
                    e.Value = _tradeBookStore.GetCellValue(actualRowIndex, e.ColumnIndex);
                }
                else
                {
                    // Use alive rows list to skip deleted rows
                    if (_allAliveTradeRows.Count == 0 || e.RowIndex >= _allAliveTradeRows.Count)
                    {
                        e.Value = null;
                        return;
                    }
                    int actualRowIndex = _allAliveTradeRows[e.RowIndex];
                    e.Value = _tradeBookStore.GetCellValue(actualRowIndex, e.ColumnIndex);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error retrieving Trade Book cell value at ({e.RowIndex}, {e.ColumnIndex})", ex);
                e.Value = null;
            }
        }
        #endregion

        #region Search Event Handlers
        /// <summary>
        /// Handles text changes in the Order Book search box.
        /// </summary>
        private async void OrderSearchBox_TextChanged(object sender, EventArgs e)
        {
            var searchText = _orderSearchBox.Text.Trim();
            if (string.IsNullOrEmpty(searchText))
            {
                _orderSearchActive = false;
                _filteredOrderRows.Clear();
                UpdateOrderBookRowCount();
                return;
            }
            try
            {
                await Task.Run(() =>
                {
                    var idResults = _orderBookStore.Search(searchText, 0, 500);
                    var symbolResults = _orderBookStore.Search(searchText, 1, 500);
                    _filteredOrderRows = idResults.Union(symbolResults).OrderBy(x => x).ToList();
                    _orderSearchActive = true;
                    this.BeginInvoke((Action)UpdateOrderBookRowCount);
                });
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error during Order Book search for '{searchText}'", ex);
            }
        }
        /// <summary>
        /// Handles text changes in the Trade Book search box.
        /// </summary>
        private async void TradeSearchBox_TextChanged(object sender, EventArgs e)
        {
            var searchText = _tradeSearchBox.Text.Trim();
            if (string.IsNullOrEmpty(searchText))
            {
                _tradeSearchActive = false;
                _filteredTradeRows.Clear();
                UpdateTradeBookRowCount();
                return;
            }
            try
            {
                await Task.Run(() =>
                {
                    var idResults = _tradeBookStore.Search(searchText, 0, 500);
                    var symbolResults = _tradeBookStore.Search(searchText, 1, 500);
                    _filteredTradeRows = idResults.Union(symbolResults).OrderBy(x => x).ToList();
                    _tradeSearchActive = true;
                    this.BeginInvoke((Action)UpdateTradeBookRowCount);
                });
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error during Trade Book search for '{searchText}'", ex);
            }
        }
        #endregion

        #region UI Update Methods
        /// <summary>
        /// Updates the Order Book DataGridView row count and statistics label.
        /// </summary>
        private void UpdateOrderBookRowCount()
        {
            try
            {
                // Track render start time for metrics
                var renderStart = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                int displayRowCount;
                if (_orderSearchActive)
                {
                    // If search active and 0 matches, show 0 rows
                    displayRowCount = _filteredOrderRows.Count == 0 ? 0 : _filteredOrderRows.Count;
                }
                else
                {
                    // Use alive rows count to skip deleted entries
                    displayRowCount = _allAliveOrderRows.Count;
                }
                _orderBookGrid.RowCount = displayRowCount;
                _orderStatsLabel.Text = _orderSearchActive ?
                    $"Showing: {displayRowCount:N0} (of {_orderBookStore.RowCount:N0})" :
                    $"Rows: {displayRowCount:N0}";
                if (DateTime.UtcNow - _lastOrderGridUpdate > TimeSpan.FromMilliseconds(UI_UPDATE_INTERVAL_MS))
                {
                    _orderBookGrid.Invalidate();
                    _lastOrderGridUpdate = DateTime.UtcNow;
                }
                // Track render completion for metrics
                var renderEnd = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                _metricsLogger.UpdateRenderTiming(renderStart, renderEnd);
            }
            catch (Exception ex)
            {
                _logger.LogError("Error updating Order Book row count", ex);
            }
        }
        /// <summary>
        /// Updates the Trade Book DataGridView row count and statistics label.
        /// </summary>
        private void UpdateTradeBookRowCount()
        {
            try
            {
                // Track render start time for metrics
                var renderStart = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                int displayRowCount;
                if (_tradeSearchActive)
                {
                    // If search active and 0 matches, show 0 rows
                    displayRowCount = _filteredTradeRows.Count == 0 ? 0 : _filteredTradeRows.Count;
                }
                else
                {
                    // Use alive rows count to skip deleted entries
                    displayRowCount = _allAliveTradeRows.Count;
                }
                _tradeBookGrid.RowCount = displayRowCount;
                _tradeStatsLabel.Text = _tradeSearchActive ?
                    $"Showing: {displayRowCount:N0} (of {_tradeBookStore.RowCount:N0})" :
                    $"Rows: {displayRowCount:N0}";
                if (DateTime.UtcNow - _lastTradeGridUpdate > TimeSpan.FromMilliseconds(UI_UPDATE_INTERVAL_MS))
                {
                    _tradeBookGrid.Invalidate();
                    _lastTradeGridUpdate = DateTime.UtcNow;
                }
                // Track render completion for metrics
                var renderEnd = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                _metricsLogger.UpdateRenderTiming(renderStart, renderEnd);
            }
            catch (Exception ex)
            {
                _logger.LogError("Error updating Trade Book row count", ex);
            }
        }
        #endregion

        #region Search Refresh Methods
        /// <summary>
        /// Asynchronously refreshes the Order Book search results to include newly arrived data.
        /// </summary>
        private async Task RefreshOrderSearchAsync(string searchText)
        {
            if (string.IsNullOrWhiteSpace(searchText))
            {
                _orderSearchActive = false;
                _filteredOrderRows.Clear();
                this.BeginInvoke((Action)UpdateOrderBookRowCount);
                return;
            }
            try
            {
                await Task.Run(() =>
                {
                    var idResults = _orderBookStore.Search(searchText, 0, 500);
                    var symbolResults = _orderBookStore.Search(searchText, 1, 500);
                    _filteredOrderRows = idResults.Union(symbolResults).OrderBy(x => x).ToList();
                    _orderSearchActive = true; // Remain true even if count == 0
                    this.BeginInvoke((Action)UpdateOrderBookRowCount);
                });
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error refreshing Order Book search for '{searchText}'", ex);
            }
        }

        /// <summary>
        /// Asynchronously refreshes the Trade Book search results to include newly arrived data.
        /// </summary>
        private async Task RefreshTradeSearchAsync(string searchText)
        {
            if (string.IsNullOrWhiteSpace(searchText))
            {
                _tradeSearchActive = false;
                _filteredTradeRows.Clear();
                this.BeginInvoke((Action)UpdateTradeBookRowCount);
                return;
            }

            try
            {
                await Task.Run(() =>
                {
                    var idResults = _tradeBookStore.Search(searchText, 0, 500);
                    var symbolResults = _tradeBookStore.Search(searchText, 1, 500);
                    _filteredTradeRows = idResults.Union(symbolResults).OrderBy(x => x).ToList();
                    _tradeSearchActive = true; // Keep search active even if no matches
                    this.BeginInvoke((Action)UpdateTradeBookRowCount);
                });
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error refreshing Trade Book search for '{searchText}'", ex);
            }
        }

        #endregion

        #region Alive Rows Refresh Method
        /// <summary>
        /// Rebuilds lists of all alive (non-deleted) rows to avoid displaying blank rows.
        /// Called after batch ingestion to keep UI lists current.
        /// </summary>
        private void RefreshAliveRows()
        {
            var aliveOrders = new List<int>();
            for (int i = 0; i < _orderBookStore.RowCount; i++)
            {
                var row = _orderBookStore.GetRowByIndex(i);
                if (row != null && row[0] != null)
                    aliveOrders.Add(i);
            }
            _allAliveOrderRows = aliveOrders;

            var aliveTrades = new List<int>();
            for (int i = 0; i < _tradeBookStore.RowCount; i++)
            {
                var row = _tradeBookStore.GetRowByIndex(i);
                if (row != null && row[0] != null)
                    aliveTrades.Add(i);
            }
            _allAliveTradeRows = aliveTrades;
        }
        #endregion

        #region Background Processing Event Handlers
        /// <summary>
        /// Event handler called when the ingestion worker completes processing a batch of data messages.
        /// </summary>
        private async void OnDataBatchProcessed(object sender, DataBatchProcessedEventArgs e)
        {
            try
            {
                System.Threading.Interlocked.Increment(ref _uiRenderQueueDepth);

                // Rebuild alive rows for no-search mode display filtering
                RefreshAliveRows();

                // Refresh active searches with newly arrived data
                if (_orderSearchActive)
                {
                    await RefreshOrderSearchAsync(_orderSearchBox.Text);
                }
                if (_tradeSearchActive)
                {
                    await RefreshTradeSearchAsync(_tradeSearchBox.Text);
                }

                // UI update on the main thread
                this.BeginInvoke(() =>
                {
                    if (!_orderSearchActive)
                        UpdateOrderBookRowCount();
                    if (!_tradeSearchActive)
                        UpdateTradeBookRowCount();
                    _statusLabel.Text = $"Processed: " +
                                       $"Orders: Inserts: +{e.OrderBookInserts}/ Updates: ~{e.OrderBookUpdates}/ Deletions: -{e.OrderBookDeletes}, " +
                                       $"Trades: Inserts: +{e.TradeBookInserts}/ Updates: ~{e.TradeBookUpdates}/ Deletions: -{e.TradeBookDeletes}";
                    _orderBookGrid.Invalidate();
                    _tradeBookGrid.Invalidate();
                    System.Threading.Interlocked.Decrement(ref _uiRenderQueueDepth);
                });
            }
            catch (Exception ex)
            {
                _logger.LogError("Error processing data batch update", ex);
                System.Threading.Interlocked.Decrement(ref _uiRenderQueueDepth);
            }
        }
        /// <summary>
        /// Timer tick handler that updates the metrics display in the status strip.
        /// </summary>
        private void UiUpdateTimer_Tick(object sender, EventArgs e)
        {
            try
            {
                var stats = _metricsLogger.GetCurrentStats();
                var ingestStats = _ingestWorker.GetStats();
                // Update UI render queue depth in metrics
                stats.UiRenderQueueDepth = _uiRenderQueueDepth;
                _metricsLabel.Text = $"Throughput: {stats.ThroughputMsgPerSec:F1}/sec | " +
                                    $"E2E P99: {stats.EndToEndLatencyP99}ms | " +
                                    $"Memory: {stats.ProcessMemoryMB}MB | " +
                                    $"Queue: {ingestStats.QueueDepth} | " +
                                    $"UI Queue: {_uiRenderQueueDepth}";
            }
            catch (Exception ex)
            {
                // Don't log UI timer errors to prevent spam
                System.Diagnostics.Debug.WriteLine($"UI timer error: {ex.Message}");
            }
        }
        #endregion

        #region Form Lifecycle
        /// <summary>
        /// Handles form closing event and performs cleanup of resources.
        /// </summary>
        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            _logger.LogInformation("MainForm shutdown initiated");
            try
            {
                _uiUpdateTimer?.Stop();
                _ingestWorker?.Dispose();
                _metricsLogger?.Dispose();
                _logger.LogInformation("MainForm shutdown completed");
            }
            catch (Exception ex)
            {
                _logger.LogError("Error during form shutdown", ex);
            }
            base.OnFormClosing(e);
        }
        #endregion
    }
}
