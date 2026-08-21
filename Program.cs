
using CloudFlare.Client;
using CloudFlare.Client.Api.Zones.DnsRecord;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.FileProviders;
using System.Text;
using System.Threading.RateLimiting;
using CloudFlare.Client.Enumerators;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Snowflakes;
using SuperCoolWebServer.Auth;
using SuperCoolWebServer.Data;
using SuperCoolWebServer.Models;
using tusdotnet;
using tusdotnet.Interfaces;
using tusdotnet.Models;
using tusdotnet.Models.Configuration;

namespace SuperCoolWebServer
{
    public class Program
    {
                              //const string? ADDRESS = null;
        // const string? ADDRESS = "http://localhost:9009/";
        //const string? ADDRESS = "https://extraes.xyz/";
        public static void Main(string[] args)
        {
            InitAsync().GetAwaiter().GetResult();

            var builder = WebApplication.CreateBuilder(args);

            // Add services to the container.

            builder.Services.AddControllers();
            
            // Adding the snowflake gen doesn't need the ServiceProvider, but it's good to have if I switch to an
            // injected config or program information system to like, get a sharded instance ID, it will come in handy
            builder.Services.AddSingleton(static serviceProvider =>
            {
                const int INSTANCE_ID = 0;
                DateTime Epoch = new DateTime(2026, 1, 1,  0, 0, 0, DateTimeKind.Utc);

                var snowflakeGen = SnowflakeGenerator.CreateBuilder()
                        .AddConstant(1, 0) // So it's always positive
                        .AddBlockingTimestamp(53, Epoch, TimeSpan.TicksPerMillisecond)
                        .AddConstant(8, INSTANCE_ID)
                        .AddConstant(1, 0) // So it's always even
                        .Build();
                return snowflakeGen;
            });
            
            // Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen();
            builder.Services.AddDbContext<DataContext>();
            builder.Services.AddDatabaseDeveloperPageExceptionFilter();

            builder.Services.AddAuthorization(options =>
            {
                var allPerms = Enum.GetValues<Permissions>();

                foreach (var permission in allPerms)
                {
                    if (permission is Permissions.Administrator or 0)
                        continue;
                    
                    var requirement = new PermissionRequirement(permission);
                    options.AddPolicy(permission.ToString(), policy => policy.AddRequirements(requirement));
                }
            });
            builder.Services.AddSingleton<IAuthorizationHandler, PermissionAuthorizationHandler>();

            builder.Services.ConfigureApplicationCookie(options =>
            {
                options.Cookie.Name = builder.Environment.IsDevelopment()
                    ? "SuperCoolAuth"
                    : "__Host-SuperCoolAuth";
                options.Cookie.HttpOnly = true;
                options.Cookie.SecurePolicy = builder.Environment.IsDevelopment()
                    ? CookieSecurePolicy.SameAsRequest
                    : CookieSecurePolicy.Always;
                options.Cookie.SameSite = SameSiteMode.Strict;

                options.LoginPath = "/login";
                options.AccessDeniedPath = "/forbidden";

                options.ExpireTimeSpan = TimeSpan.FromDays(14);
                options.SlidingExpiration = true;
            });
            
            builder.Services.AddIdentity<SuperCoolUser, IdentityRole<long>>(options =>
                {
                    options.User.RequireUniqueEmail = false; // dont use email
                    // ReSharper disable once StringLiteralTypo
                    options.User.AllowedUserNameCharacters = "qwertyuiopasdfghjklzxcvbnmQWERTYUIOPASDFGHJKLZXCVBNM1234567890-_";

                    options.Password.RequiredLength = 12;
                    options.Password.RequireDigit = true;
                    options.Password.RequireUppercase = true;
                    options.Password.RequireNonAlphanumeric = true;
                })
                .AddEntityFrameworkStores<DataContext>()
                .AddClaimsPrincipalFactory<PermissionsToClaimsPrincipalFactory>()
                .AddDefaultTokenProviders();
            
            builder.Services.AddMvc(opt =>
            {
                string[] mimeTypes =
                [
                    "image/gif",
                    "application/octet-stream",
                    "video/mp4",
                    "video/webm"
                ];
                foreach (var mimeType in mimeTypes)
                {
                    opt.InputFormatters.Add(new RawRequestBodyFormatter(mimeType));
                }
                // opt.InputFormatters.Insert(0, new RawRequestBodyFormatter("image/gif"));
                opt.AllowEmptyInputInBodyModelBinding = false;
            });

            RateLimiters.SetupRateLimiters(builder);

            var app = builder.Build();
            app.UseRateLimiter();

            // Configure the HTTP request pipeline.
            if (app.Environment.IsDevelopment())
            {
                // I'm ACTIVELY DEVELOPING and Firefox decides to CACHE LOCALHOST.
                // Fucking moronic.
                app.Use(async (ctx, next) =>
                {
                    // MSDN says to not write to response directly from middleware.
                    // SRC: https://learn.microsoft.com/en-us/aspnet/core/fundamentals/middleware/
                    ctx.Response.OnStarting(() =>
                    {
                        ctx.Response.Headers.CacheControl = "max-age=0";
                        return Task.CompletedTask;
                    });
                    await next.Invoke();
                });
                
                // Give Swagger the ability to get an antiforgery token
                app.MapGet("/_af/token", (HttpContext ctx, IAntiforgery antiforgery) =>
                {
                    var tokens = antiforgery.GetAndStoreTokens(ctx);
                    ctx.Response.Headers.CacheControl = "no-store";

                    return Results.Ok(new
                    {
                        token = tokens.RequestToken
                    });
                })
                .AllowAnonymous()
                .RequireRateLimiting(RateLimiters.FIXED);
                
                app.UseDeveloperExceptionPage();
                app.UseMigrationsEndPoint();
                app.UseSwagger();
                app.UseSwaggerUI(opt =>
                {
                    opt.ConfigObject.AdditionalItems["withCredentials"] = true;
                    opt.UseRequestInterceptor("""
                        function (request) {
                            console.log(request);
                            /*
                            Not every request has the "method" field set.
                            (For example: The very first req to get the swagger.json definition)
                            ((also this has use multiline comment syntax because this all gets flattened into one line))
                            */
                            if (!request.method)
                                return;
                            const method = request.method.toUpperCase();
                            const safeMethods = ["GET", "HEAD", "OPTIONS", "TRACE"];

                            if (safeMethods.includes(method)) {
                              return request;
                            }

                            return fetch("/_af/token", {
                              credentials: "same-origin",
                              cache: "no-store"
                            })
                            .then(function (response) {
                              if (!response.ok) {
                                  throw new Error(
                                      "Could not obtain antiforgery token: " +
                                      response.status
                                  );
                              }

                              return response.json();
                            })
                            .then(function (body) {
                              request.headers = request.headers || {};
                              request.headers["RequestVerificationToken"] = body.token;

                              return request;
                            });
                        }
                        """.Replace('"','\'').ReplaceLineEndings(" "));
                    // String manip is needed there because Swashbuckle stupidly outputs malformed JSON otherwise.
                });
            }
            else
            {
                app.UseExceptionHandler("/Error");
            }

            using (var scope = app.Services.CreateScope())
            {
                var services = scope.ServiceProvider;
                var context = services.GetRequiredService<DataContext>();
                context.Database.Migrate();
                
                DataHelper.Initialize(services.GetRequiredService<UserManager<SuperCoolUser>>(),
                    services.GetRequiredService<SnowflakeGenerator<long>>())
                    .GetAwaiter().GetResult();
            }

            if (!app.Environment.IsDevelopment())
                app.UseHttpsRedirection();
            
            app.UseAuthentication();
            app.UseAuthorization();
            app.UseAntiforgery();
            
            app.MapControllers();

            ConfigureTus(app);

            app.UseStaticFiles(new StaticFileOptions()
            {
                FileProvider = new PhysicalFileProvider(Path.GetFullPath("./frontend")),
                RequestPath = "/frontend"
            });
            app.MapStaticAssets();

            if (app.Environment.IsDevelopment())
                app.Run();
            else
                app.Run(Config.values.listenOn);
        }

        private static void ConfigureTus(WebApplication app)
        {
            if (!Directory.Exists(Path.Combine(Config.values.filestoreDir, "tus")))
                Directory.CreateDirectory(Path.Combine(Config.values.filestoreDir, "tus"));

            app.MapTus("/files", async httpCtx => {
                
                httpCtx.Features.Get<IHttpMaxRequestBodySizeFeature>()!.MaxRequestBodySize = 1024 * 1024 * 30;
                return new DefaultTusConfiguration
                {
                    Store = new tusdotnet.Stores.TusDiskStore(Path.Combine(Config.values.filestoreDir, "tus")),
                    Events = new Events
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

                            await using var fileStream2 = new FileStream(Path.Combine(Config.values.filestoreDir, filename), FileMode.Create, FileAccess.Write);
                            await fileStream.CopyToAsync(fileStream2);
                            
                        }
                    }
                };
            })
            .RequireRateLimiting(RateLimiters.STRICT)
            .RequireAuthorization(nameof(Permissions.UploadFiles));
        }

        static async Task InitAsync()
        {
            await SetCloudflareIpAsync();
            await YoutubeDLSharp.Utils.DownloadFFmpeg();
            await YoutubeDLSharp.Utils.DownloadYtDlp();
        }

        static async Task SetCloudflareIpAsync()
        {
            if (string.IsNullOrEmpty(Config.values.cloudflareKey))
                return;
            
            using HttpClient clint = new();
            var myIp = await clint.GetStringAsync("https://icanhazip.com");
            myIp = myIp.Trim();
            
            using CloudFlareClient cf = new(Config.values.cloudflareKey);

            // var zoneMapName = new Dictionary<string, Zone>();
            // var zoneMapId = new Dictionary<string, Zone>();
            // var zones = await cf.Zones.GetAsync();
            // foreach (var zone in zones.Result)
            // {
            //     Logger.Put($"Found zone '{zone.Name}' (ID {zone.Id})");
            //     zoneMapName[zone.Name] = zone;
            //     zoneMapId[zone.Id] = zone;
            // }
            //zones.Result.First().dns
            var dnsRecords = await cf.Zones.DnsRecords.GetAsync(Config.values.cloudflareZoneId);

            foreach (var record in dnsRecords.Result)
            {
                if (!Config.values.cloudflareDnsEntryNames.Contains(record.Name))
                {
                    Logger.Put($"Skipping DNS record {record.Name} (ID {record.Id}) as it is not in the config", LogType.Debug);
                    continue;
                }

                Logger.Put($"Found a DNS {record.Type} record {record.Name} on zone '{record.ZoneName}' (ZID {record.ZoneId}) with IP {record.Content}", LogType.Debug);
                
                if (record.Content == myIp)
                {
                    Logger.Put("IP is already set to " + myIp, LogType.Debug);
                    continue;
                }

                if (record.Type != DnsRecordType.A)
                {
                    Logger.Put("Record is not a DNS A record. Ignoring.", LogType.Debug);
                    continue;
                }
                
                // string zoneId = record.ZoneId;
                // if (string.IsNullOrWhiteSpace(zoneId))
                // {
                //     // According to docs & nullability hinting, they should never be null. But they are. CF killed my grandma
                //     if (record.ZoneName is not null && zoneMapName.TryGetValue(record.ZoneName, out var zone))
                //         zoneId = zone.Id;
                //     else if (record.ZoneId is not null && zoneMapId.TryGetValue(record.Id, out zone))
                //         zoneId = zone.Id;
                //     else if (zoneMapId.Values.Count == 1)
                //     {
                //         Logger.Warn("Using ZoneID fallback-fallback (there's only one zone, so we're gonna use its id).");
                //         zoneId = zoneMapId.Values.First().Id;
                //     }
                //     else
                //         Logger.Warn("Failed to map back to the original DNS record's ZoneID.");
                // }

                ModifiedDnsRecord moddedDns = new()
                {
                    Name = record.Name,
                    Type = record.Type,
                    Content = myIp,
                    Proxied = record.Proxied,
                    Ttl = record.Ttl,
                };
                var res = await cf.Zones.DnsRecords.UpdateAsync(Config.values.cloudflareZoneId, record.Id, moddedDns);
                if (res.Success)
                    Logger.Put($"Successfully updated CloudFlare DNS for record '{record.Name}' (ID {record.Id})!");
                else
                    Logger.Warn($"Unable to update CloudFlare DNS record '{record.Name}' (ID {record.Id})!!!!! {res.Errors[0].Message}");
            }
        }
    }
}