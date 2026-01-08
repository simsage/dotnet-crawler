namespace Crawlers;

/// <summary>
/// Represents the startup parameters required for configuring and initializing
/// a SimSage Crawler instance.
/// </summary>
public class StartParameters
{
    public string SimSageEndpoint { get; private set; } = "";
    public string SourceType { get; private set; } = "";
    public const string SimSageApiVersion = "1";
    public string OrganisationId { get; private set; } = "";
    public string KbId { get; private set; } = "";
    public string Sid { get; private set; } = "";
    public string Aes { get; private set; } = "";
    public int SourceId { get; private set; } = -1;
    public bool UseEncryption { get; set; } = false;
    public bool ExitAfterFinishing { get; private set; }
    public bool AllowSelfSignedCertificate { get; private set; } = true;
    public bool UseCache { get; private set; } = true;

    /// <summary>
    /// read parameters from args[]
    /// </summary>
    /// <param name="args"></param>
    /// <returns></returns>
    /// <exception cref="Exception"></exception>
    public bool ProcessParameters(string[] args)
    {
        if (args.Length < 12)
        {
            WriteParameters();
            return false;
        }

        var i = 0;
        while (i < args.Length)
        {
            if (args[i] == "-server" && i + 1 < args.Length)
            {
                SimSageEndpoint = args[i + 1];
                i += 1;
            }
            else if (args[i] == "-org" && i + 1 < args.Length)
            {
                OrganisationId = args[i + 1];
                i += 1;
            }
            else if (args[i] == "-kb" && i + 1 < args.Length)
            {
                KbId = args[i + 1];
                i += 1;
            }
            else if (args[i] == "-sid" && i + 1 < args.Length)
            {
                Sid = args[i + 1];
                i += 1;
            }
            else if (args[i] == "-aes" && i + 1 < args.Length)
            {
                Aes = args[i + 1];
                i += 1;
            }
            else if (args[i] == "-source" && i + 1 < args.Length)
            {
                if (!int.TryParse(args[i + 1], out var sourceId) || sourceId is <= 0 or > 65535)
                {
                    throw new Exception($"Invalid source id: {args[i + 1]}");
                }

                SourceId = sourceId;
                i += 1;
            }
            else if (args[i] == "-crawler" && i + 1 < args.Length)
            {
                SourceType = args[i + 1];
                i += 1;
            }
            else if (args[i] == "-encryption")
            {
                UseEncryption = true;
            }
            else if (args[i] == "-noselfsigned")
            {
                AllowSelfSignedCertificate = false;
            }
            else if (args[i] == "-exitwhendone")
            {
                ExitAfterFinishing = true;
            }
            else if (args[i] == "-disablecache")
            {
                UseCache = false;
            }
            else
            {
                Console.WriteLine($"Unknown parameter: {args[i]}");
                WriteParameters();
                return false;
            }
            i += 1;
        }

        if (SimSageEndpoint.Length == 0 || !SimSageEndpoint.EndsWith("/api") || SourceType != "file" ||
            OrganisationId.Length == 0 || KbId.Length == 0 || Sid.Length == 0 || SourceId <= 0 ||
            !(SimSageEndpoint.StartsWith("https://") || SimSageEndpoint.StartsWith("http://")))
        {
            WriteParameters();
            return false;
        }

        return true;
    }

    /// <summary>
    /// Writes information about the required and optional parameters for configuring
    /// the crawler. It outputs guidance on how to use parameters like server URL,
    /// organization ID, knowledge-base ID, and other configurations to the console.
    /// </summary>
    private static void WriteParameters()
    {
        Console.WriteLine("parameters:");
        Console.WriteLine("    -server https://test7.simsage.ai/api          # required: the SimSage server to communicate with");
        Console.WriteLine("    -org 0197e48f-be19-541f-59d5-fe44613836a8     # required: the organisation ID");
        Console.WriteLine("    -kb 0197e490-5781-bab6-5347-279d6704eec4      # required: the knowledge-base ID");
        Console.WriteLine("    -sid eb1d0e94-bd2d-7b3d-8fe7-01df99d72ce1     # required: the security ID");
        Console.WriteLine("    -crawler file                                 # required: the type of the Source, must be \"file\"");
        Console.WriteLine("    -source 1                                     # required: the source ID");
        Console.WriteLine("    -aes 313939                                   # optional: AES decryption string (only needed of -encryption is used)");
        Console.WriteLine("    -encryption                                   # optional: use message encryption (requires -aes to be used)");
        Console.WriteLine("    -noselfsigned                                 # optional: don't allow self-signed certificates");
        Console.WriteLine("    -exitwhendone                                 # optional: exit the crawler when done");
        Console.WriteLine("    -disablecache                                 # optional: do not use the sqlite asset cache");
    }
    
}
