//
// Program.cs - Application entry point with dependency injection
//

using System;
using System.Windows.Forms;
using TradingDataVisualization;
using TradingDataVisualization.Infrastructure;
using TradingDataVisualization.Logs;

namespace TradingDataVisualization
{
    /// <summary>
    /// Main entry point for the Trading Data Visualization application
    /// </summary>
    internal static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            // Enable visual styles and DPI awareness for modern Windows
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.SetHighDpiMode(HighDpiMode.SystemAware);

            try
            {
                // Create logger instance
                ILogger logger = new FileLogger();
                var mainForm = new MainForm(logger);
                Application.Run(mainForm);

                logger.LogInformation("=== Application startup initiated ===");

                // Run the main form with dependency injection
                Application.Run(new MainForm(logger));

                logger.LogInformation("=== Application shutdown completed ===");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Application startup error: {ex.Message}",
                    "Startup Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}
