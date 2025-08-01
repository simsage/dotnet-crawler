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

            // Log that the service is starting
            var singleStringSpace = string.Join(" ", args);
            EventLog.WriteEntry(InternalServiceName, $"Service is starting, parameters: {singleStringSpace}", EventLogEntryType.Information);

            // Parse command line arguments
            if (args is { Length: >= 12 })
            {
                var startParameters = new StartParameters();
                if (!startParameters.ProcessParameters(args))
                {
                    var errStr3 = "Bad Starting Parameters";
                    EventLog.WriteEntry(InternalServiceName, errStr3, EventLogEntryType.Error);
                    throw new InvalidOperationException(errStr3);
                }

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
                    EventLog.WriteEntry(InternalServiceName, errStr1, EventLogEntryType.Error);
                    throw new InvalidOperationException(errStr1);
                }
                if (source.CrawlerType != Source.CT_FILE)
                {
                    var errStr2 = $"Source {source.Name} (id {source.SourceId}) is not a Microsoft File Crawler";
                    EventLog.WriteEntry(InternalServiceName, errStr2, EventLogEntryType.Error);
                    throw new InvalidOperationException(errStr2);
                }
                EventLog.WriteEntry(InternalServiceName, $"{source}: starting new {source.CrawlerType} external-crawler", EventLogEntryType.Information);

                // run this thing threaded
                Task.Run(() =>
                {
                    crawler.Active = true;
                    while (crawler.Active)
                    {
                        crawler.Initialize(InternalServiceName, source.GetCrawlerPropertyMap(), platform);
                        platform.CrawlerStart(crawler);
                        if (platform.WaitForStart() && crawler.Run())
                        {
                            platform.CrawlerDone();
                            EventLog.WriteEntry(InternalServiceName, $"{source}: done", EventLogEntryType.Information);
                        }
                        else
                        {
                            platform.CrawlerCrashed("");
                            EventLog.WriteEntry(InternalServiceName, $"{source}: TERMINATED", EventLogEntryType.Error);
                        }
                        if (startParameters.ExitAfterFinishing)
                        {
                            break;
                        }
                        EventLog.WriteEntry(InternalServiceName, $"{source}: waiting five minutes before resuming", EventLogEntryType.Information);
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
                EventLog.WriteEntry(InternalServiceName, errStr4, EventLogEntryType.Error);
                throw new InvalidOperationException(errStr4);
            }
        }

        protected override void OnStop()
        {
            EventLog.WriteEntry(InternalServiceName, "Service is stopping.", EventLogEntryType.Information);
            crawler.Active = false;
            if (platform != null)
                platform.Active = false;
        }
        
        protected override void OnShutdown()
        {
            EventLog.WriteEntry(InternalServiceName, "Service is shutting down.", EventLogEntryType.Information);
            crawler.Active = false;
            if (platform != null)
                platform.Active = false;
        }
        
    }
#pragma warning restore CA1416
