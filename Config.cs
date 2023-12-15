using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Tomlet;
using Tomlet.Attributes;

namespace SuperCoolWebServer;

internal class Config
{
    public static event Action? ConfigChanged;
    public static Config values;
    private const string CFG_PATH = "./config.toml";
    public string logPath = "./logs/";

    static readonly HashAlgorithm hasher = SHA256.Create();

    public int maxLogFiles = 5;

    public string cloudflareKey = "";
    public string cloudflareZoneId = "";
    public string cloudflareDnsEntryName = "extraes.xyz";
    //public int cloudflareDdnsIntervalHours = 6;

    // Filestorage
    public string filestoreDir = "./filestore/";
    public string filestoreAuth = "REPLACEME";

    // Link
    public string redirectAuth = "REPLACEME";

    // IP Access
    public string ipAccessAuth = "REPLACEME";

    static Config()
    {
        Console.WriteLine("Initializing config");
        if (!File.Exists(CFG_PATH))
        {
            File.WriteAllText(CFG_PATH, TomletMain.TomlStringFrom(new Config())); // mmm triple parenthesis, v nice
        }

        ReadConfig();
        WriteConfig();
    }

    public static void OutputRawTOML()
    {
        Console.WriteLine(File.ReadAllText(CFG_PATH));
    }

    [MemberNotNull(nameof(values))]
    public static void ReadConfig()
    {
        string configText = File.ReadAllText(CFG_PATH);
        values = TomletMain.To<Config>(configText);
    }

    public static void WriteConfig()
    {
        File.WriteAllText(CFG_PATH, TomletMain.TomlStringFrom(values));
        ConfigChanged?.Invoke();
    }

    // https://stackoverflow.com/questions/8820399/c-sharp-4-0-how-to-get-64-bit-hash-code-of-given-string
    public static long Hash(string str)
    {
        var bytes = hasher.ComputeHash(Encoding.Default.GetBytes(str));
        Array.Resize(ref bytes, bytes.Length + bytes.Length % 8); //make multiple of 8 if hash is not, for exampel SHA1 creates 20 bytes. 
        return Enumerable.Range(0, bytes.Length / 8) // create a counter for de number of 8 bytes in the bytearray
            .Select(i => BitConverter.ToInt64(bytes, i * 8)) // combine 8 bytes at a time into a integer
            .Aggregate((x, y) => x ^ y); //xor the bytes together so you end up with a long (64-bit int)
    }
}
