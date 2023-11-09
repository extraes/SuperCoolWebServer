using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
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
    public string gifsiclePath = "./gifsicle/";

    public string quoteFont = "Comfortaa";

    public ulong[] blockedUsers = Array.Empty<ulong>();
    public ulong[] owners = Array.Empty<ulong>();
    public ulong[] requiredRoles = Array.Empty<ulong>();
    public ulong[] requiredEmojis = Array.Empty<ulong>();
    public ulong outputChannel = 0;
    public int maxLogFiles = 5;
    

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
}
