namespace Crawlers;

using System;
using System.Configuration.Install;
using System.Linq;
using System.Reflection;
using System.ServiceProcess;

#pragma warning disable CA1416
    static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        static void Main(string[] args)
        {
            // Check if the application is running interactively (from the command line)
            // or as a service (started by SCM).
            bool isInteractive = Environment.UserInteractive;

            // Arguments for console mode or installation/uninstallation
            string[] consoleArgs = args;

            // If running interactively, check for special commands like /install, /uninstall, /console
            if (isInteractive)
            {
                if (consoleArgs.Length > 0)
                {
                    switch (consoleArgs[0].ToLowerInvariant())
                    {
                        case "/install":
                            var adjustedArgs1 = consoleArgs.Skip(1).ToArray();
                            InstallService(adjustedArgs1);
                            return;
                        case "/uninstall":
                            UninstallService();
                            return;
                        case "/console":
                        case "/debug":
                            // Run the service logic directly in the console
                            var adjustedArgs = consoleArgs.Skip(1).ToArray();
                            var startParameters = new StartParameters();
                            if (!startParameters.ProcessParameters(adjustedArgs))
                            {
                                Console.WriteLine("Bad Starting Parameters");
                            }
                            var service = new WindowsCrawlerService();
                            service.InternalServiceName = "Console";
                            // Pass any remaining arguments to the service's internal Start method
                            service.StartFromConsole(adjustedArgs);
                            Console.WriteLine("Service stopped.");
                            return;
                        default:
                            Console.WriteLine("Unknown command-line argument. Use /install, /uninstall, /console, or /debug.");
                            Console.WriteLine("If running as a service, arguments are passed via 'sc start <servicename> [args]'.");
                            return;
                    }
                }
                // If no arguments in interactive mode, prompt or just run normally as a service executable
                Console.WriteLine("No special command-line arguments provided. To run in console mode, use /console or /debug.");
                Console.WriteLine("To install, use /install. To uninstall, use /uninstall.");
                Console.WriteLine("Press any key to exit.");
                Console.Read();
            }
            else
            {
                // If not interactive, run as a Windows Service
                var svc = new WindowsCrawlerService();
                svc.AlternateArgs = args;
                ServiceBase[] servicesToRun = [svc];
                ServiceBase.Run(servicesToRun);
            }
        }

        // --- Helper Methods for Installation/Uninstallation ---

        private static void InstallService(string[]? serviceBinPathArgs)
        {
            try
            {
                Console.WriteLine("Installing service...");
                var installerArgs = new List<string>();
                // The first argument for ManagedInstallerClass.InstallHelper must be the assembly path
                installerArgs.Add(Assembly.GetExecutingAssembly().Location);
                // Add the service's default binPath arguments if any
                if (serviceBinPathArgs is { Length: > 0 })
                {
                    installerArgs.AddRange(serviceBinPathArgs);
                }
                // Use the ManagedInstallerClass to install the service
                ManagedInstallerClass.InstallHelper(installerArgs.ToArray());
                Console.WriteLine("Service installed successfully.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error installing service: {ex.Message}");
                if (ex.InnerException != null)
                {
                    Console.WriteLine($"Inner Exception: {ex.InnerException.Message}");
                }
            }
        }

        private static void UninstallService()
        {
            try
            {
                Console.WriteLine("Uninstalling service...");
                // Use the ManagedInstallerClass to uninstall the service
                ManagedInstallerClass.InstallHelper(["/u", Assembly.GetExecutingAssembly().Location]);
                Console.WriteLine("Service uninstalled successfully.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error uninstalling service: {ex.Message}");
                if (ex.InnerException != null)
                {
                    Console.WriteLine($"Inner Exception: {ex.InnerException.Message}");
                }
            }
        }
    }
#pragma warning restore CA1416
