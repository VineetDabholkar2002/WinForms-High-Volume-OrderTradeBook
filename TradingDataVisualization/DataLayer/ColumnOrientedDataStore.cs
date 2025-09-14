//
// ColumnOrientedDataStore.cs - High-performance columnar data storage
// Optimized for fast lookups, updates, and memory efficiency
//

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using TradingDataVisualization.DataLayer;

namespace TradingDataVisualization.DataLayer
{
    /// <summary>
    /// High-performance column-oriented data store optimized for real-time trading data
    /// Supports up to 2M rows with 50 columns, fast key-based lookups and updates
    /// </summary>
    public class ColumnOrientedDataStore<T> where T : class
    {
        private readonly Dictionary<string, int> _keyToRowIndex;
        private readonly List<object[]> _columnData;
        private readonly ReaderWriterLockSlim _lock;
        private readonly string[] _columnNames;
        private readonly Func<T, string> _keyExtractor;
        private readonly Func<T, object[]> _dataExtractor;
        private readonly Func<string, T> _csvParser;
        private volatile int _rowCount;
        private const int MAX_ROWS = 2_000_000;
        
        /// <summary>
        /// Current number of rows in the data store
        /// </summary>
        public int RowCount => _rowCount;
        
        /// <summary>
        /// Column names for the data store
        /// </summary>
        public string[] ColumnNames => _columnNames;
        
        /// <summary>
        /// Maximum capacity of the data store
        /// </summary>
        public int MaxCapacity => MAX_ROWS;
        
        /// <summary>
        /// Initializes a new column-oriented data store
        /// </summary>
        /// <param name="columnNames">Array of column names (must be exactly 50)</param>
        /// <param name="keyExtractor">Function to extract the key from data objects</param>
        /// <param name="dataExtractor">Function to convert objects to object arrays</param>
        /// <param name="csvParser">Function to parse CSV strings to objects</param>
        public ColumnOrientedDataStore(
            string[] columnNames,
            Func<T, string> keyExtractor,
            Func<T, object[]> dataExtractor,
            Func<string, T> csvParser)
        {
            if (columnNames?.Length != 50)
                throw new ArgumentException("Column names array must contain exactly 50 elements");
            
            _columnNames = columnNames;
            _keyExtractor = keyExtractor ?? throw new ArgumentNullException(nameof(keyExtractor));
            _dataExtractor = dataExtractor ?? throw new ArgumentNullException(nameof(dataExtractor));
            _csvParser = csvParser ?? throw new ArgumentNullException(nameof(csvParser));
            
            _keyToRowIndex = new Dictionary<string, int>(MAX_ROWS);
            _columnData = new List<object[]>(MAX_ROWS);
            _lock = new ReaderWriterLockSlim();
            _rowCount = 0;
        }
        
        /// <summary>
        /// Inserts or updates a row in the data store
        /// </summary>
        /// <param name="item">The item to insert or update</param>
        /// <returns>True if inserted, false if updated</returns>
        public bool InsertOrUpdate(T item)
        {
            if (item == null) return false;
            
            var key = _keyExtractor(item);
            var data = _dataExtractor(item);
            
            if (data?.Length != 50)
                throw new ArgumentException("Data array must contain exactly 50 elements");
            
            _lock.EnterWriteLock();
            try
            {
                if (_keyToRowIndex.TryGetValue(key, out var existingIndex))
                {
                    // Update existing row
                    _columnData[existingIndex] = data;
                    return false;
                }
                else
                {
                    // Insert new row
                    if (_rowCount >= MAX_ROWS)
                        throw new InvalidOperationException($"Data store capacity exceeded. Maximum rows: {MAX_ROWS}");
                    
                    var newIndex = _rowCount;
                    _keyToRowIndex[key] = newIndex;
                    _columnData.Add(data);
                    Interlocked.Increment(ref _rowCount);
                    return true;
                }
            }
            finally
            {
                _lock.ExitWriteLock();
            }
        }
        
        /// <summary>
        /// Inserts or updates multiple rows efficiently in batch
        /// </summary>
        /// <param name="items">Collection of items to process</param>
        /// <returns>Number of items inserted (vs updated)</returns>
        public int BatchInsertOrUpdate(IEnumerable<T> items)
        {
            if (items == null) return 0;
            
            var insertCount = 0;
            var itemList = items.ToList();
            
            _lock.EnterWriteLock();
            try
            {
                foreach (var item in itemList)
                {
                    if (item == null) continue;
                    
                    var key = _keyExtractor(item);
                    var data = _dataExtractor(item);
                    
                    if (data?.Length != 50) continue;
                    
                    if (_keyToRowIndex.TryGetValue(key, out var existingIndex))
                    {
                        // Update existing row
                        _columnData[existingIndex] = data;
                    }
                    else
                    {
                        // Insert new row
                        if (_rowCount >= MAX_ROWS) break;
                        
                        var newIndex = _rowCount;
                        _keyToRowIndex[key] = newIndex;
                        _columnData.Add(data);
                        _rowCount++;
                        insertCount++;
                    }
                }
            }
            finally
            {
                _lock.ExitWriteLock();
            }
            
            return insertCount;
        }
        
        /// <summary>
        /// Deletes a row from the data store
        /// </summary>
        /// <param name="key">Key of the row to delete</param>
        /// <returns>True if deleted, false if not found</returns>
        public bool Delete(string key)
        {
            if (string.IsNullOrEmpty(key)) return false;
            
            _lock.EnterWriteLock();
            try
            {
                if (!_keyToRowIndex.TryGetValue(key, out var index))
                    return false;
                
                // Mark row as deleted by setting first column to null
                // This is more efficient than actually removing the row
                _columnData[index][0] = null;
                _keyToRowIndex.Remove(key);
                
                return true;
            }
            finally
            {
                _lock.ExitWriteLock();
            }
        }
        
        /// <summary>
        /// Gets a row by its key
        /// </summary>
        /// <param name="key">The key to search for</param>
        /// <returns>Row data as object array, or null if not found</returns>
        public object[] GetRowByKey(string key)
        {
            if (string.IsNullOrEmpty(key)) return null;
            
            _lock.EnterReadLock();
            try
            {
                if (_keyToRowIndex.TryGetValue(key, out var index))
                {
                    var row = _columnData[index];
                    // Check if row is deleted (first column is null)
                    return row[0] == null ? null : row;
                }
                return null;
            }
            finally
            {
                _lock.ExitReadLock();
            }
        }
        
        /// <summary>
        /// Gets a row by its index (for DataGridView virtual mode)
        /// </summary>
        /// <param name="rowIndex">The zero-based row index</param>
        /// <returns>Row data as object array, or null if invalid index</returns>
        public object[] GetRowByIndex(int rowIndex)
        {
            if (rowIndex < 0 || rowIndex >= _rowCount) return null;
            
            _lock.EnterReadLock();
            try
            {
                if (rowIndex < _columnData.Count)
                {
                    var row = _columnData[rowIndex];
                    // Check if row is deleted (first column is null)
                    return row[0] == null ? new object[50] : row; // Return empty row for deleted entries
                }
                return null;
            }
            finally
            {
                _lock.ExitReadLock();
            }
        }
        
        /// <summary>
        /// Gets a specific cell value by row and column index
        /// </summary>
        /// <param name="rowIndex">Zero-based row index</param>
        /// <param name="columnIndex">Zero-based column index</param>
        /// <returns>Cell value or null if invalid indices</returns>
        public object GetCellValue(int rowIndex, int columnIndex)
        {
            if (rowIndex < 0 || rowIndex >= _rowCount || columnIndex < 0 || columnIndex >= 50)
                return null;
            
            _lock.EnterReadLock();
            try
            {
                if (rowIndex < _columnData.Count)
                {
                    var row = _columnData[rowIndex];
                    return row[0] == null ? null : row[columnIndex]; // Return null for deleted rows
                }
                return null;
            }
            finally
            {
                _lock.ExitReadLock();
            }
        }
        
        /// <summary>
        /// Searches for rows matching a filter criteria
        /// </summary>
        /// <param name="searchText">Text to search for</param>
        /// <param name="keyColumnIndex">Index of the key column (0 for ID, 1 for Symbol typically)</param>
        /// <param name="maxResults">Maximum number of results to return</param>
        /// <returns>List of matching row indices</returns>
        public List<int> Search(string searchText, int keyColumnIndex = 0, int maxResults = 1000)
        {
            var results = new List<int>();
            if (string.IsNullOrEmpty(searchText) || keyColumnIndex < 0 || keyColumnIndex >= 50)
                return results;
            
            var searchLower = searchText.ToLowerInvariant();
            
            _lock.EnterReadLock();
            try
            {
                for (int i = 0; i < Math.Min(_rowCount, _columnData.Count) && results.Count < maxResults; i++)
                {
                    var row = _columnData[i];
                    if (row[0] == null) continue; // Skip deleted rows
                    
                    var cellValue = row[keyColumnIndex]?.ToString()?.ToLowerInvariant();
                    if (!string.IsNullOrEmpty(cellValue) && cellValue.Contains(searchLower))
                    {
                        results.Add(i);
                    }
                }
            }
            finally
            {
                _lock.ExitReadLock();
            }
            
            return results;
        }
        
        /// <summary>
        /// Gets memory usage statistics
        /// </summary>
        /// <returns>Memory usage information</returns>
        public MemoryStats GetMemoryStats()
        {
            _lock.EnterReadLock();
            try
            {
                var keyMemory = _keyToRowIndex.Count * (24 + 16); // Estimated overhead per key-value pair
                var dataMemory = _columnData.Count * 50 * 8; // Estimated 8 bytes per object reference
                var totalMemory = keyMemory + dataMemory;
                
                return new MemoryStats
                {
                    KeyIndexMemoryBytes = keyMemory,
                    DataMemoryBytes = dataMemory,
                    TotalMemoryBytes = totalMemory,
                    RowCount = _rowCount,
                    Capacity = MAX_ROWS,
                    MemoryUtilization = (double)totalMemory / (MAX_ROWS * 50 * 8)
                };
            }
            finally
            {
                _lock.ExitReadLock();
            }
        }
        
        /// <summary>
        /// Parses CSV data and performs batch insert/update
        /// </summary>
        /// <param name="csvData">CSV data string</param>
        /// <returns>Number of rows processed</returns>
        public int ProcessCsvData(string csvData)
        {
            if (string.IsNullOrEmpty(csvData)) return 0;
            
            var lines = csvData.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            var items = new List<T>();
            
            foreach (var line in lines)
            {
                try
                {
                    var item = _csvParser(line.Trim());
                    if (item != null)
                        items.Add(item);
                }
                catch
                {
                    // Skip invalid lines
                    continue;
                }
            }
            
            BatchInsertOrUpdate(items);
            return items.Count;
        }
        
        /// <summary>
        /// Clears all data from the store
        /// </summary>
        public void Clear()
        {
            _lock.EnterWriteLock();
            try
            {
                _keyToRowIndex.Clear();
                _columnData.Clear();
                _rowCount = 0;
            }
            finally
            {
                _lock.ExitWriteLock();
            }
        }
        
        /// <summary>
        /// Disposes the data store and releases resources
        /// </summary>
        public void Dispose()
        {
            _lock?.Dispose();
        }
    }
    
    /// <summary>
    /// Memory usage statistics for the data store
    /// </summary>
    public class MemoryStats
    {
        public long KeyIndexMemoryBytes { get; set; }
        public long DataMemoryBytes { get; set; }
        public long TotalMemoryBytes { get; set; }
        public int RowCount { get; set; }
        public int Capacity { get; set; }
        public double MemoryUtilization { get; set; }
        
        public string GetFormattedStats()
        {
            return $"Memory: {TotalMemoryBytes / 1024 / 1024:F1} MB, " +
                   $"Rows: {RowCount:N0}/{Capacity:N0}, " +
                   $"Utilization: {MemoryUtilization:P1}";
        }
    }
}