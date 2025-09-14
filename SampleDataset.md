# Sample Dataset for Trading Data Visualization

This file contains sample trading data in CSV format for testing the high-performance data visualization system.

## Order Book Sample Data

```csv
ORD1234567,AAPL,Buy,150.25,1000,2024-01-15 09:30:00.123,Active,Limit,DAY,149.50,150.00,0,1000,0.00,NYSE,CLIENT_1001,ACC_12345,TRADER_101,Momentum,PORT_1,500000.00,75000.00,RG_1,15025.00,USD,150.20,150.30,150.25,6.7,2000,1500,150.22,1250000,150.18,TAG1,TAG2,TAG3,TAG4,TAG5,TAG6,TAG7,TAG8,TAG9,TAG10,125.50,89.75,234.12,67.33,445.67,1001,2002,3003,2024-01-15 09:30:00.123
ORD2345678,MSFT,Sell,330.75,500,2024-01-15 09:30:01.456,Active,Market,IOC,0.00,330.75,250,250,330.70,NASDAQ,CLIENT_1002,ACC_23456,TRADER_102,Arbitrage,PORT_2,750000.00,165375.00,RG_2,16537.50,USD,330.70,330.80,330.75,3.0,800,600,330.72,987500,330.68,TAGB1,TAGB2,TAGB3,TAGB4,TAGB5,TAGB6,TAGB7,TAGB8,TAGB9,TAGB10,298.45,412.33,156.78,289.12,367.89,2001,3002,4003,2024-01-15 09:30:01.456
ORD3456789,GOOGL,Buy,2750.50,100,2024-01-15 09:30:02.789,Partial,Limit,GTC,2745.00,2750.50,75,25,2749.25,ARCA,CLIENT_1003,ACC_34567,TRADER_103,MarketMaking,PORT_3,1000000.00,275050.00,RG_3,27505.00,USD,2750.25,2750.75,2750.50,1.8,150,125,2750.40,567800,2750.15,TAGC1,TAGC2,TAGC3,TAGC4,TAGC5,TAGC6,TAGC7,TAGC8,TAGC9,TAGC10,1899.67,2156.88,3567.22,1234.56,987.34,3001,4002,5003,2024-01-15 09:30:02.789
```

## Trade Book Sample Data

```csv
TRD7890123,AAPL,Buy,150.25,500,2024-01-15 09:30:05.123,Executed,ORD1234567,ORD9876543,0.75,0.15,75124.10,2024-01-17,CLEAR_01,NYSE,BUYER_1001,SELLER_2002,BACC_12345,SACC_67890,BROKER_101,RG_1,37562.05,Cleared,Reported,USD,150.22,0.03,0.0012,1250000,150.18,150.20,Normal,TTAG1,TTAG2,TTAG3,TTAG4,TTAG5,TTAG6,TTAG7,TTAG8,TTAG9,TTAG10,125.67,234.89,456.12,789.34,321.56,5001,6002,7003,2024-01-15 09:30:05.123
TRD8901234,MSFT,Sell,330.75,250,2024-01-15 09:30:06.456,Executed,ORD2345678,ORD8765432,0.83,0.17,82686.00,2024-01-17,CLEAR_02,NASDAQ,BUYER_2001,SELLER_1002,BACC_23456,SACC_78901,BROKER_102,RG_2,41343.00,Cleared,Reported,USD,330.70,0.05,0.0015,987500,330.68,330.72,Normal,TTAG1B,TTAG2B,TTAG3B,TTAG4B,TTAG5B,TTAG6B,TTAG7B,TTAG8B,TTAG9B,TTAG10B,298.45,567.89,123.45,890.12,345.67,6001,7002,8003,2024-01-15 09:30:06.456
TRD9012345,GOOGL,Buy,2750.50,75,2024-01-15 09:30:07.789,Executed,ORD3456789,ORD7654321,20.63,4.12,206233.25,2024-01-17,CLEAR_03,ARCA,BUYER_3001,SELLER_2003,BACC_34567,SACC_89012,BROKER_103,RG_3,103116.63,Cleared,Reported,USD,2749.25,1.25,0.0045,567800,2750.15,2749.85,Normal,TTAG1C,TTAG2C,TTAG3C,TTAG4C,TTAG5C,TTAG6C,TTAG7C,TTAG8C,TTAG9C,TTAG10C,1899.67,3456.78,2134.56,4567.89,1678.90,7001,8002,9003,2024-01-15 09:30:07.789
```

## Data Generation Commands

To generate larger datasets using the simulator:

### High-Volume Test (10K messages/second)
```bash
TradingDataSimulator.exe --rate 10000 --port 9999 --order-ratio 0.7
```

### Stress Test (50K messages/second)  
```bash
TradingDataSimulator.exe --rate 50000 --port 9999 --order-ratio 0.8
```

### Named Pipe Test
```bash
TradingDataSimulator.exe --pipe-only --pipe TradingDataPipe --rate 5000
```

### Mixed Load Test
```bash
# Terminal 1 - TCP
TradingDataSimulator.exe --rate 3000 --port 9999

# Terminal 2 - Named Pipe (start after main app)  
TradingDataSimulator.exe --pipe-only --pipe TradingDataPipe --rate 2000
```

## Message Format Details

### OrderBook Message Format (CSV)
```
MessageType: OrderBook
Operation: Insert/Update/Delete  
SendTimestamp: Unix milliseconds
Data: 50 comma-separated fields as defined in OrderBookEntry.cs
```

### TradeBook Message Format (CSV)
```
MessageType: TradeBook
Operation: Insert/Update/Delete
SendTimestamp: Unix milliseconds  
Data: 50 comma-separated fields as defined in TradeBookEntry.cs
```

### TCP Message Example
```
OrderBook,Insert,1705312205123,ORD1234567,AAPL,Buy,150.25,1000,2024-01-15 09:30:05.123,Active,Limit,DAY,149.50,150.00,0,1000,0.00,NYSE,CLIENT_1001,ACC_12345,TRADER_101,Momentum,PORT_1,500000.00,75000.00,RG_1,15025.00,USD,150.20,150.30,150.25,6.7,2000,1500,150.22,1250000,150.18,TAG1,TAG2,TAG3,TAG4,TAG5,TAG6,TAG7,TAG8,TAG9,TAG10,125.50,89.75,234.12,67.33,445.67,1001,2002,3003,2024-01-15 09:30:05.123
```

## Performance Test Scenarios

### Scenario 1: Steady State Load
- Rate: 1,000 messages/second
- Duration: 10 minutes  
- Mix: 70% OrderBook, 30% TradeBook
- Expected: Stable latency, minimal GC pressure

### Scenario 2: Burst Load
- Rate: 10,000 messages/second for 30 seconds
- Then: 1,000 messages/second sustained
- Expected: Queue buildup and recovery

### Scenario 3: Memory Stress
- Rate: 5,000 messages/second
- Duration: Until 2M rows reached
- Expected: Memory usage stabilization

### Scenario 4: Search Performance  
- Load: 1M+ rows
- Search: Real-time filtering while ingesting
- Expected: <50ms search response time

## Validation Data

The sample data includes realistic values for:
- **Stock Symbols**: Mix of real (AAPL, MSFT, GOOGL) and synthetic symbols
- **Prices**: Market-realistic price ranges and movements
- **Quantities**: Trading block sizes and fractional fills  
- **Timestamps**: Microsecond precision timestamps
- **Market Data**: Bid/ask spreads, volumes, VWAP calculations
- **Risk Fields**: Exposure amounts, margin requirements
- **Metadata**: Client IDs, trader IDs, portfolio assignments

Use this sample data to validate:
1. Correct parsing of all 50 columns
2. Search functionality across ID and Symbol fields
3. Performance under various load conditions
4. UI responsiveness during data ingestion
5. Metrics collection and CSV logging accuracy