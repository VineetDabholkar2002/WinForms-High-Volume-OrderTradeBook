# I'll create the project structure and organize the code files

project_structure = {
    "MainApplication": [
        "Program.cs",
        "MainForm.cs", 
        "MainForm.Designer.cs"
    ],
    "DataLayer": [
        "ColumnOrientedDataStore.cs",
        "DataMessage.cs",
        "OrderBookEntry.cs",
        "TradeBookEntry.cs"
    ],
    "Workers": [
        "IngestWorker.cs",
        "GridRenderer.cs",
        "MetricsLogger.cs"
    ],
    "Infrastructure": [
        "PerformanceMetrics.cs",
        "SearchManager.cs",
        "TcpMessageHandler.cs"
    ],
    "Simulator": [
        "DataSimulator.cs",
        "SimulatorProgram.cs"
    ],
    "Configuration": [
        "AppSettings.cs"
    ]
}

print("Project Structure:")
for folder, files in project_structure.items():
    print(f"\n{folder}/")
    for file in files:
        print(f"  - {file}")
        
print(f"\nTotal files to create: {sum(len(files) for files in project_structure.values())}")