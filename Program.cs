
using CloudFlare.Client;
using CloudFlare.Client.Api.Zones.DnsRecord;
using Microsoft.AspNetCore.RateLimiting;

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
            using HttpClient clint = new();
            myIp = clint.GetStringAsync("https://icanhazip.com").GetAwaiter().GetResult().Trim();
            SetCloudflareIp();

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
            if (app.Environment.IsDevelopment())
            {
                app.UseSwagger();
                app.UseSwaggerUI();
            }

            app.UseHttpsRedirection();

            app.UseAuthorization();

            app.MapControllers();

            //app.Run("http://" + myIp + ":9009/");
            app.Run("http://192.168.86.26:9009/");
        }

        static void SetCloudflareIp()
        {
            if (string.IsNullOrEmpty(Config.values.cloudflareKey))
                return;
            using HttpClient clint = new();
            using CloudFlareClient cf = new(Config.values.cloudflareKey);

            var zones = cf.Zones.GetAsync().GetAwaiter().GetResult();
            //zones.Result.First().dns
            var dnsRecords = cf.Zones.DnsRecords.GetAsync(Config.values.cloudflareZoneId).GetAwaiter().GetResult();

            var dns = dnsRecords.Result.FirstOrDefault(d => d.Type == CloudFlare.Client.Enumerators.DnsRecordType.A && d.Name == Config.values.cloudflareDnsEntryName)
                ?? throw new Exception("DNS entry not found! Make sure it's an A record with your provided name!");

            ModifiedDnsRecord moddedDns = new()
            {
                Name = dns.Name,
                Type = dns.Type,
                Content = myIp,
                Proxied = dns.Proxied,
                Ttl = dns.Ttl,
            };
            var res = cf.Zones.DnsRecords.UpdateAsync(dns.ZoneId, dns.Id, moddedDns).GetAwaiter().GetResult();
            if (res.Success)
                Logger.Put("Successfully updated CloudFlare DNS!");
            else
                Logger.Warn("Unable to update CloudFlare DNS!!!!! " + res.Errors[0].Message);
        }
    }
}