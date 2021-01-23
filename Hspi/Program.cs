namespace Hspi
{
    /// <summary>
    /// Class for the main program.
    /// </summary>
    public static class Program
    {
        private static void Main(string[] args)
        {
            Logger.ConfigureLogging(false, false);
            logger.Info("Starting...");

            try
            {
                using (var plugin = new HSPI_Tasmota.HSPI())
                {
                    plugin.Connect(args);
                }
            }
            finally
            {
                logger.Info("Bye!!!");
            }
        }

        private static NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();
    }
}