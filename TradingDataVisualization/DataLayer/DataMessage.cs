//
// DataMessage.cs - Core data structures for message processing
// High-Performance WinForms Trading Data Visualization System
//

using System;
using System.Text.Json;

namespace TradingDataVisualization.DataLayer
{
    /// <summary>
    /// Base message for all data ingestion operations
    /// </summary>
    public class DataMessage
    {
        public long SendTimestamp { get; set; }
        public long ReceiveTimestamp { get; set; }
        public long QueueTimestamp { get; set; }
        public long ApplyTimestamp { get; set; }
        public long RenderStartTimestamp { get; set; }
        public long RenderEndTimestamp { get; set; }
        public MessageType Type { get; set; }
        public DataOperation Operation { get; set; }
        public string Data { get; set; } = string.Empty;
        
        /// <summary>
        /// Calculates end-to-end latency from send to render completion
        /// </summary>
        public long GetEndToEndLatency()
        {
            return RenderEndTimestamp - SendTimestamp;
        }
        
        /// <summary>
        /// Gets processing latency (receive to apply)
        /// </summary>
        public long GetProcessingLatency()
        {
            return ApplyTimestamp - ReceiveTimestamp;
        }
        
        /// <summary>
        /// Gets render latency (render start to end)
        /// </summary>
        public long GetRenderLatency()
        {
            return RenderEndTimestamp - RenderStartTimestamp;
        }
    }

    /// <summary>
    /// Message types for routing data to appropriate grids
    /// </summary>
    public enum MessageType
    {
        OrderBook,
        TradeBook
    }

    /// <summary>
    /// Data operations for CRUD operations
    /// </summary>
    public enum DataOperation
    {
        Insert,
        Update,
        Delete
    }

    /// <summary>
    /// Order book entry structure with 50 columns
    /// </summary>
    public class OrderBookEntry
    {
        // Core identification fields
        public string OrderId { get; set; } = string.Empty;
        public string Symbol { get; set; } = string.Empty;
        public string Side { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public long Quantity { get; set; }
        public DateTime Timestamp { get; set; }
        public string Status { get; set; } = string.Empty;
        
        // Trading fields
        public string OrderType { get; set; } = string.Empty;
        public string TimeInForce { get; set; } = string.Empty;
        public decimal StopPrice { get; set; }
        public decimal LimitPrice { get; set; }
        public long FilledQuantity { get; set; }
        public long RemainingQuantity { get; set; }
        public decimal AvgFillPrice { get; set; }
        public string Exchange { get; set; } = string.Empty;
        
        // Client/Account fields
        public string ClientId { get; set; } = string.Empty;
        public string AccountId { get; set; } = string.Empty;
        public string TraderId { get; set; } = string.Empty;
        public string Strategy { get; set; } = string.Empty;
        public string Portfolio { get; set; } = string.Empty;
        
        // Risk management fields
        public decimal RiskLimit { get; set; }
        public decimal ExposureAmount { get; set; }
        public string RiskGroup { get; set; } = string.Empty;
        public decimal MarginRequirement { get; set; }
        public string Currency { get; set; } = string.Empty;
        
        // Market data fields
        public decimal BidPrice { get; set; }
        public decimal AskPrice { get; set; }
        public decimal MidPrice { get; set; }
        public decimal SpreadBps { get; set; }
        public long BidSize { get; set; }
        public long AskSize { get; set; }
        public decimal LastPrice { get; set; }
        public long Volume { get; set; }
        public decimal VWAP { get; set; }
        
        // Additional metadata fields (to reach 50 columns)
        public string Tag1 { get; set; } = string.Empty;
        public string Tag2 { get; set; } = string.Empty;
        public string Tag3 { get; set; } = string.Empty;
        public string Tag4 { get; set; } = string.Empty;
        public string Tag5 { get; set; } = string.Empty;
        public string Tag6 { get; set; } = string.Empty;
        public string Tag7 { get; set; } = string.Empty;
        public string Tag8 { get; set; } = string.Empty;
        public string Tag9 { get; set; } = string.Empty;
        public string Tag10 { get; set; } = string.Empty;
        public decimal Value1 { get; set; }
        public decimal Value2 { get; set; }
        public decimal Value3 { get; set; }
        public decimal Value4 { get; set; }
        public decimal Value5 { get; set; }
        public long Counter1 { get; set; }
        public long Counter2 { get; set; }
        public long Counter3 { get; set; }
        public DateTime UpdateTime { get; set; }

        /// <summary>
        /// Converts OrderBookEntry to object array for DataGridView virtual mode
        /// </summary>
        public object[] ToObjectArray()
        {
            return new object[]
            {
                OrderId, Symbol, Side, Price, Quantity, Timestamp, Status,
                OrderType, TimeInForce, StopPrice, LimitPrice, FilledQuantity,
                RemainingQuantity, AvgFillPrice, Exchange, ClientId, AccountId,
                TraderId, Strategy, Portfolio, RiskLimit, ExposureAmount, RiskGroup,
                MarginRequirement, Currency, BidPrice, AskPrice, MidPrice, SpreadBps,
                BidSize, AskSize, LastPrice, Volume, VWAP, Tag1, Tag2, Tag3, Tag4,
                Tag5, Tag6, Tag7, Tag8, Tag9, Tag10, Value1, Value2, Value3, Value4,
                Value5, Counter1
            };
        }


        /// <summary>
        /// Creates OrderBookEntry from CSV string
        /// </summary>
        public static OrderBookEntry FromCsv(string csvLine)
        {
            var fields = csvLine.Split(',');
            if (fields.Length != 50)
                throw new ArgumentException("CSV line must have exactly 50 fields");

            return new OrderBookEntry
            {
                OrderId = fields[0],
                Symbol = fields[1],
                Side = fields[2],
                Price = decimal.TryParse(fields[3], out var price) ? price : 0,
                Quantity = long.TryParse(fields[4], out var qty) ? qty : 0,
                Timestamp = DateTime.TryParse(fields[5], out var ts) ? ts : DateTime.MinValue,
                Status = fields[6],
                OrderType = fields[7],
                TimeInForce = fields[8],
                StopPrice = decimal.TryParse(fields[9], out var stopPrice) ? stopPrice : 0,
                LimitPrice = decimal.TryParse(fields[10], out var limitPrice) ? limitPrice : 0,
                FilledQuantity = long.TryParse(fields[11], out var filled) ? filled : 0,
                RemainingQuantity = long.TryParse(fields[12], out var remaining) ? remaining : 0,
                AvgFillPrice = decimal.TryParse(fields[13], out var avgFill) ? avgFill : 0,
                Exchange = fields[14],
                ClientId = fields[15],
                AccountId = fields[16],
                TraderId = fields[17],
                Strategy = fields[18],
                Portfolio = fields[19],
                RiskLimit = decimal.TryParse(fields[20], out var riskLimit) ? riskLimit : 0,
                ExposureAmount = decimal.TryParse(fields[21], out var exposure) ? exposure : 0,
                RiskGroup = fields[22],
                MarginRequirement = decimal.TryParse(fields[23], out var margin) ? margin : 0,
                Currency = fields[24],
                BidPrice = decimal.TryParse(fields[25], out var bid) ? bid : 0,
                AskPrice = decimal.TryParse(fields[26], out var ask) ? ask : 0,
                MidPrice = decimal.TryParse(fields[27], out var mid) ? mid : 0,
                SpreadBps = decimal.TryParse(fields[28], out var spread) ? spread : 0,
                BidSize = long.TryParse(fields[29], out var bidSize) ? bidSize : 0,
                AskSize = long.TryParse(fields[30], out var askSize) ? askSize : 0,
                LastPrice = decimal.TryParse(fields[31], out var lastPrice) ? lastPrice : 0,
                Volume = long.TryParse(fields[32], out var volume) ? volume : 0,
                VWAP = decimal.TryParse(fields[33], out var vwap) ? vwap : 0,
                Tag1 = fields[34],
                Tag2 = fields[35],
                Tag3 = fields[36],
                Tag4 = fields[37],
                Tag5 = fields[38],
                Tag6 = fields[39],
                Tag7 = fields[40],
                Tag8 = fields[41],
                Tag9 = fields[42],
                Tag10 = fields[43],
                Value1 = decimal.TryParse(fields[44], out var val1) ? val1 : 0,
                Value2 = decimal.TryParse(fields[45], out var val2) ? val2 : 0,
                Value3 = decimal.TryParse(fields[46], out var val3) ? val3 : 0,
                Value4 = decimal.TryParse(fields[47], out var val4) ? val4 : 0,
                Value5 = decimal.TryParse(fields[48], out var val5) ? val5 : 0,
                Counter1 = long.TryParse(fields[49], out var counter1) ? counter1 : 0,
            };
        }

    }

    /// <summary>
    /// Trade book entry structure with 50 columns
    /// </summary>
    public class TradeBookEntry
    {
        // Core identification fields
        public string TradeId { get; set; } = string.Empty;
        public string Symbol { get; set; } = string.Empty;
        public string Side { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public long Quantity { get; set; }
        public DateTime Timestamp { get; set; }
        public string Status { get; set; } = string.Empty;
        
        // Trade specific fields
        public string BuyOrderId { get; set; } = string.Empty;
        public string SellOrderId { get; set; } = string.Empty;
        public decimal Commission { get; set; }
        public decimal Fees { get; set; }
        public decimal NetAmount { get; set; }
        public string SettlementDate { get; set; } = string.Empty;
        public string ClearingFirm { get; set; } = string.Empty;
        public string Exchange { get; set; } = string.Empty;
        
        // Counterparty fields
        public string BuyerId { get; set; } = string.Empty;
        public string SellerId { get; set; } = string.Empty;
        public string BuyerAccount { get; set; } = string.Empty;
        public string SellerAccount { get; set; } = string.Empty;
        public string ExecutingBroker { get; set; } = string.Empty;
        
        // Risk and compliance fields
        public string RiskGroup { get; set; } = string.Empty;
        public decimal ExposureImpact { get; set; }
        public string ComplianceStatus { get; set; } = string.Empty;
        public string RegReportingStatus { get; set; } = string.Empty;
        public string Currency { get; set; } = string.Empty;
        
        // Market context fields
        public decimal MarketPrice { get; set; }
        public decimal PriceDeviation { get; set; }
        public decimal MarketImpact { get; set; }
        public long MarketVolume { get; set; }
        public decimal VWAP { get; set; }
        public decimal TWAPPrice { get; set; }
        public string TradeCondition { get; set; } = string.Empty;
        
        // Additional metadata fields (to reach 50 columns)
        public string Tag1 { get; set; } = string.Empty;
        public string Tag2 { get; set; } = string.Empty;
        public string Tag3 { get; set; } = string.Empty;
        public string Tag4 { get; set; } = string.Empty;
        public string Tag5 { get; set; } = string.Empty;
        public string Tag6 { get; set; } = string.Empty;
        public string Tag7 { get; set; } = string.Empty;
        public string Tag8 { get; set; } = string.Empty;
        public string Tag9 { get; set; } = string.Empty;
        public string Tag10 { get; set; } = string.Empty;
        public decimal Value1 { get; set; }
        public decimal Value2 { get; set; }
        public decimal Value3 { get; set; }
        public decimal Value4 { get; set; }
        public decimal Value5 { get; set; }
        public long Counter1 { get; set; }
        public long Counter2 { get; set; }
        public long Counter3 { get; set; }
        public DateTime UpdateTime { get; set; }

        /// <summary>
        /// Converts TradeBookEntry to object array for DataGridView virtual mode
        /// </summary>
        public object[] ToObjectArray()
        {
            return new object[]
            {
                TradeId, Symbol, Side, Price, Quantity, Timestamp, Status,
                BuyOrderId, SellOrderId, Commission, Fees, NetAmount, SettlementDate,
                ClearingFirm, Exchange, BuyerId, SellerId, BuyerAccount, SellerAccount,
                ExecutingBroker, RiskGroup, ExposureImpact, ComplianceStatus,
                RegReportingStatus, Currency, MarketPrice, PriceDeviation, MarketImpact,
                MarketVolume, VWAP, TWAPPrice, TradeCondition, Tag1, Tag2, Tag3, Tag4,
                Tag5, Tag6, Tag7, Tag8, Tag9, Tag10, Value1, Value2, Value3, Value4,
                Value5, Counter1, Counter2, Counter3
            };
        }




        /// <summary>
        /// Creates TradeBookEntry from CSV string
        /// </summary>
        public static TradeBookEntry FromCsv(string csvLine)
        {
            var fields = csvLine.Split(',');
            if (fields.Length != 50)
                throw new ArgumentException("CSV line must have exactly 50 fields");

            return new TradeBookEntry
            {
                TradeId = fields[0],
                Symbol = fields[1],
                Side = fields[2],
                Price = decimal.TryParse(fields[3], out var price) ? price : 0,
                Quantity = long.TryParse(fields[4], out var qty) ? qty : 0,
                Timestamp = DateTime.TryParse(fields[5], out var ts) ? ts : DateTime.MinValue,
                Status = fields[6],
                BuyOrderId = fields[7],
                SellOrderId = fields[8],
                Commission = decimal.TryParse(fields[9], out var commission) ? commission : 0,
                Fees = decimal.TryParse(fields[10], out var fees) ? fees : 0,
                NetAmount = decimal.TryParse(fields[11], out var netAmount) ? netAmount : 0,
                SettlementDate = fields[12],
                ClearingFirm = fields[13],
                Exchange = fields[14],
                BuyerId = fields[15],
                SellerId = fields[16],
                BuyerAccount = fields[17],
                SellerAccount = fields[18],
                ExecutingBroker = fields[19],
                RiskGroup = fields[20],
                ExposureImpact = decimal.TryParse(fields[21], out var exposure) ? exposure : 0,
                ComplianceStatus = fields[22],
                RegReportingStatus = fields[23],
                Currency = fields[24],
                MarketPrice = decimal.TryParse(fields[25], out var marketPrice) ? marketPrice : 0,
                PriceDeviation = decimal.TryParse(fields[26], out var deviation) ? deviation : 0,
                MarketImpact = decimal.TryParse(fields[27], out var impact) ? impact : 0,
                MarketVolume = long.TryParse(fields[28], out var volume) ? volume : 0,
                VWAP = decimal.TryParse(fields[29], out var vwap) ? vwap : 0,
                TWAPPrice = decimal.TryParse(fields[30], out var twap) ? twap : 0,
                TradeCondition = fields[31],
                Tag1 = fields[32],
                Tag2 = fields[33],
                Tag3 = fields[34],
                Tag4 = fields[35],
                Tag5 = fields[36],
                Tag6 = fields[37],
                Tag7 = fields[38],
                Tag8 = fields[39],
                Tag9 = fields[40],
                Tag10 = fields[41],
                Value1 = decimal.TryParse(fields[42], out var val1) ? val1 : 0,
                Value2 = decimal.TryParse(fields[43], out var val2) ? val2 : 0,
                Value3 = decimal.TryParse(fields[44], out var val3) ? val3 : 0,
                Value4 = decimal.TryParse(fields[45], out var val4) ? val4 : 0,
                Value5 = decimal.TryParse(fields[46], out var val5) ? val5 : 0,
                Counter1 = long.TryParse(fields[47], out var counter1) ? counter1 : 0,
                Counter2 = long.TryParse(fields[48], out var counter2) ? counter2 : 0,
                Counter3 = long.TryParse(fields[49], out var counter3) ? counter3 : 0
            };
        }


    }
}