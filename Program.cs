
using CloudFlare.Client;
using CloudFlare.Client.Api.Zones.DnsRecord;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.FileProviders;
using System.Text;
using tusdotnet;
using tusdotnet.Interfaces;
using tusdotnet.Models.Configuration;

namespace SuperCoolWebServer
{
    public class Program
    {
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
        static string myIp;
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.

                              //const string? ADDRESS = null;
        const string? ADDRESS = "http://localhost:9009/";
        //const string? ADDRESS = "https://extraes.xyz/";
        public static void Main(string[] args)
        {
            InitAsync().GetAwaiter().GetResult();

            var builder = WebApplication.CreateBuilder(args);

            // Add services to the container.

            builder.Services.AddControllers();
            // Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen();
            builder.Services.AddMvc(opt => opt.InputFormatters.Insert(0, new RawRequestBodyFormatter("image/gif")));
            builder.Services.AddMvc(opt => opt.InputFormatters.Insert(0, new RawRequestBodyFormatter("application/octet-stream")));
            builder.Services.AddMvc(opt => opt.InputFormatters.Insert(0, new RawRequestBodyFormatter("video/mp4")));
            builder.Services.AddMvc(opt => opt.InputFormatters.Insert(0, new RawRequestBodyFormatter("video/webm")));
            builder.Services.AddRateLimiter(_ =>
            {
                _.AddFixedWindowLimiter("fixed", opt =>
                {
                    opt.PermitLimit = 4;
                    opt.Window = TimeSpan.FromSeconds(10);
                    opt.QueueProcessingOrder = System.Threading.RateLimiting.QueueProcessingOrder.OldestFirst;
                    opt.QueueLimit = 2;
                });
            });

            var app = builder.Build();

            // Configure the HTTP request pipeline.
            //if (app.Environment.IsDevelopment())
            //{
            app.UseSwagger();
            app.UseSwaggerUI();
            //}

            app.UseHttpsRedirection();

            app.UseAuthorization();

            app.MapControllers();

            if (!Directory.Exists(Path.Combine(Config.values.filestoreDir, "tus")))
                Directory.CreateDirectory(Path.Combine(Config.values.filestoreDir, "tus"));
            app.MapTus("/files", async httpCtx => {
                httpCtx.Features.Get<IHttpMaxRequestBodySizeFeature>()!.MaxRequestBodySize = 1024 * 1024 * 30;
                return new()
                {
                    Store = new tusdotnet.Stores.TusDiskStore(Path.Combine(Config.values.filestoreDir, "tus")),
                    Events = new()
                    {
                        OnCreateCompleteAsync = ctx =>
                        {
                            Logger.Put("Created file: " + ctx.FileId);
                            return Task.CompletedTask;
                        },
                        OnFileCompleteAsync = async ctx =>
                        {
                            ITusFile file = await ctx.GetFileAsync();
                            if (file == null)
                                return;
                            
                            var fileStream = await file.GetContentAsync(httpCtx.RequestAborted);
                            var metadata = await file.GetMetadataAsync(httpCtx.RequestAborted);

                            string filename = metadata.TryGetValue("filename", out tusdotnet.Models.Metadata? filenameMeta)
                                                ? filenameMeta.GetString(Encoding.UTF8)
                                                : "file";

                            httpCtx.Response.ContentType = metadata.TryGetValue("filetype", out tusdotnet.Models.Metadata? filetypeMeta)
                                                            ? filetypeMeta.GetString(Encoding.UTF8)
                                                            : "application/octet-stream";

                            //Providing New File name with extension
                            //string filestoreDir = @"C:\tusfiles\";

                            using var fileStream2 = new FileStream(Path.Combine(Config.values.filestoreDir, filename), FileMode.Create, FileAccess.Write);
                            await fileStream.CopyToAsync(fileStream2);
                        }
                    }
                };
            });

            app.UseStaticFiles(new StaticFileOptions()
            {
                FileProvider = new PhysicalFileProvider(Path.GetFullPath("./frontend")),
                RequestPath = "/frontend"
            });

            app.Run(Config.values.listenOn);
        }

        static async Task InitAsync()
        {
            using HttpClient clint = new();
            myIp = await clint.GetStringAsync("https://icanhazip.com");
            myIp = myIp.Trim();
            await SetCloudflareIpAsync();
            await YoutubeDLSharp.Utils.DownloadFFmpeg();
            await YoutubeDLSharp.Utils.DownloadYtDlp();
        }

        static async Task SetCloudflareIpAsync()
        {
            if (string.IsNullOrEmpty(Config.values.cloudflareKey))
                return;
            using CloudFlareClient cf = new(Config.values.cloudflareKey);

            var zones = await cf.Zones.GetAsync();
            //zones.Result.First().dns
            var dnsRecords = await cf.Zones.DnsRecords.GetAsync(Config.values.cloudflareZoneId);

            foreach (var record in dnsRecords.Result)
            {
                if (!Config.values.cloudflareDnsEntryNames.Contains(record.Name))
                {
                    Logger.Put($"Skipping DNS record {record.Name} (ID {record.Id}) as it is not in the config", LogType.Debug);
                    continue;
                }

                Logger.Put($"Found a DNS {record.Type} record {record.Name} with IP {record.Content}", LogType.Debug);

                if (record.Content == myIp)
                {
                    Logger.Put("IP is already set to " + myIp, LogType.Debug);
                    continue;
                }

                ModifiedDnsRecord moddedDns = new()
                {
                    Name = record.Name,
                    Type = record.Type,
                    Content = myIp,
                    Proxied = record.Proxied,
                    Ttl = record.Ttl,
                };
                var res = await cf.Zones.DnsRecords.UpdateAsync(record.ZoneId, record.Id, moddedDns);
                if (res.Success)
                    Logger.Put($"Successfully updated CloudFlare DNS for record '{record.Name}' (ID {record.Id})!");
                else
                    Logger.Warn($"Unable to update CloudFlare DNS record '{record.Name}' (ID {record.Id})!!!!! {res.Errors[0].Message}");
            }
        }
    }
}