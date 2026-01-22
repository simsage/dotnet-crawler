namespace Crawlers;

    using System.ServiceProcess;
    using System.Diagnostics;
    using System.Threading;
    
#pragma warning disable CA1416
    public class WindowsCrawlerService : ServiceBase
    {
        private readonly MicrosoftFileShareCrawler crawler = new MicrosoftFileShareCrawler();
        private PlatformCrawlerCommonProxy? platform;
        public string[] AlternateArgs { get; set; } = [];
        public string InternalServiceName = "";
        
        public void StartFromConsole(string[] args)
        {
            OnStart(args);
            Console.WriteLine("Press any key to stop...");
            Console.Read();
            OnStop();
        }
        
        protected override void OnStart(string[] originalArgs)
        {
            var args = AlternateArgs.Length > 0 ? AlternateArgs : originalArgs;
            // set internal service name based on service name?
            if (!string.IsNullOrEmpty(ServiceName) && InternalServiceName.Length == 0)
            {
                InternalServiceName = ServiceName;
            }

            // Parse command line arguments
            if (args is { Length: >= 12 })
            {
                var startParameters = new StartParameters();
                if (!startParameters.ProcessParameters(args))
                {
                    var errStr3 = "Bad Starting Parameters";
                    WriteMessage(false, InternalServiceName, errStr3, EventLogEntryType.Error);
                    throw new InvalidOperationException(errStr3);
                }

                // Log that the service is starting
                var singleStringSpace = string.Join(" ", args);
                WriteMessage(startParameters.UseEventLog, InternalServiceName, $"Service is starting, parameters: {singleStringSpace}", EventLogEntryType.Information);

                // set AES key?
                if (startParameters.Aes.Length > 0)
                {
                    AesEncryption.DataAesKey = startParameters.Aes;
                }

                platform = new PlatformCrawlerCommonProxy(
                    InternalServiceName,
                    startParameters.SimSageEndpoint, 
                    StartParameters.SimSageApiVersion,
                    startParameters.SourceType,
                    startParameters.OrganisationId, 
                    startParameters.KbId, 
                    startParameters.Sid, 
                    startParameters.Aes,
                    startParameters.SourceId, 
                    startParameters.UseEncryption, 
                    startParameters.ExitAfterFinishing, 
                    startParameters.AllowSelfSignedCertificate,
                    startParameters.UseCache
                );

                var source = platform.GetSource();
                if (!source.IsExternal)
                {
                    var errStr1 = $"Source {source.Name} (id {source.SourceId}) is not an external Source";
                    WriteMessage(startParameters.UseEventLog, InternalServiceName, errStr1, EventLogEntryType.Error);
                    throw new InvalidOperationException(errStr1);
                }
                if (source.CrawlerType != Source.CT_FILE)
                {
                    var errStr2 = $"Source {source.Name} (id {source.SourceId}) is not a Microsoft File Crawler";
                    WriteMessage(startParameters.UseEventLog, InternalServiceName, errStr2, EventLogEntryType.Error);
                    throw new InvalidOperationException(errStr2);
                }
                WriteMessage(startParameters.UseEventLog, InternalServiceName, $"{source}: starting new {source.CrawlerType} external-crawler", EventLogEntryType.Information);

                // run this thing threaded
                Task.Run(() =>
                {
                    crawler.Active = true;
                    while (crawler.Active)
                    {
                        crawler.Initialize(InternalServiceName, source.GetCrawlerPropertyMap(), platform);
                        if (platform.CrawlerStart(crawler))
                        {
                            if (platform.WaitForStart() && crawler.Run())
                            {
                                platform.CrawlerDone();
                                WriteMessage(startParameters.UseEventLog, InternalServiceName, $"{source}: done", EventLogEntryType.Information);
                            }
                            else
                            {
                                platform.CrawlerCrashed("");
                                WriteMessage(startParameters.UseEventLog, InternalServiceName, $"{source}: TERMINATED", EventLogEntryType.Error);
                            }
                        }

                        if (startParameters.ExitAfterFinishing)
                        {
                            break;
                        }

                        WriteMessage(startParameters.UseEventLog, InternalServiceName, $"{source}: TERMINATED", EventLogEntryType.Error);

                        WriteMessage(startParameters.UseEventLog, InternalServiceName, $"{source}: waiting five minutes before resuming", EventLogEntryType.Information);
                        var counter = 300; // 300 x 1 seconds = 5 minutes
                        while (crawler.Active && counter > 0)
                        {
                            Thread.Sleep(1_000);
                            counter -= 1;
                        }
                    }
                });
                
            }
            else
            {
                var errStr4 = "Bad Starting Parameters (no parameters)";
                WriteMessage(false, InternalServiceName, errStr4, EventLogEntryType.Error);
                throw new InvalidOperationException(errStr4);
            }
        }

        protected override void OnStop()
        {
            WriteMessage(false, InternalServiceName, "Service is stopping.", EventLogEntryType.Information);
            crawler.Active = false;
            if (platform != null)
                platform.Active = false;
        }
        
        protected override void OnShutdown()
        {
            WriteMessage(false, InternalServiceName, "Service is shutting down.", EventLogEntryType.Information);
            crawler.Active = false;
            if (platform != null)
                platform.Active = false;
        }


        private void WriteMessage(bool useEventLog, string internalServiceName, string message, EventLogEntryType logType)
        {
            if (useEventLog)
            {
                EventLog.WriteEntry(internalServiceName, message, logType);
            }
            else
            {
                System.Console.WriteLine($"{internalServiceName}: {logType} - {message}");
            }
        }
        
    }
#pragma warning restore CA1416
