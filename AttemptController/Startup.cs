using System;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using Microsoft.WindowsAzure.Storage;
using AttemptController.Clients;
using AttemptController.Controllers;
using AttemptController.DataStructures;
using AttemptController.Interfaces;
using AttemptController.Models;
using AttemptController.AccountStorage.Memory;

namespace AttemptController
{
    public class Startup
    {
        public Startup(IHostingEnvironment env)
        {
            var builder = new ConfigurationBuilder()
                .SetBasePath(env.ContentRootPath)
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
                .AddJsonFile($"appsettings.{env.EnvironmentName}.json", optional: true)
                .AddEnvironmentVariables();
            Configuration = builder.Build();
        }

        public IConfigurationRoot Configuration { get; }

        public void ConfigureServices(IServiceCollection services)
        {
            services.AddMvc();

            BlockingAlgorithmOptions options = new BlockingAlgorithmOptions();

            services.AddSingleton<BlockingAlgorithmOptions>(x => options);

            RemoteHost localHost = new RemoteHost { Uri = new Uri("http://localhost:35358") };
            services.AddSingleton<RemoteHost>(x => localHost);

            MaxWeightHashing<RemoteHost> hosts = new MaxWeightHashing<RemoteHost>(Configuration["Data:UniqueConfigurationSecretPhrase"]);
            hosts.Add("localhost", localHost);
            services.AddSingleton<IDistributedResponsibilitySet<RemoteHost>>(x => hosts);

            string cloudStorageConnectionString = Configuration["Data:StorageConnectionString"];
            CloudStorageAccount cloudStorageAccount = CloudStorageAccount.Parse(cloudStorageConnectionString);
            services.AddSingleton<CloudStorageAccount>(a => cloudStorageAccount);

            services.AddSingleton<MemoryUsageLimiter, MemoryUsageLimiter>();

            if (hosts.Count > 0)
            {
                DistributedBinomialLadderFilterClient dblfClient = new DistributedBinomialLadderFilterClient(
                    options.NumberOfVirtualNodesForDistributedBinomialLadder,
                    options.HeightOfBinomialLadder_H,
                    hosts,
                    options.PrivateConfigurationKey,
                    options.MinimumBinomialLadderFilterCacheFreshness);
                services.AddSingleton<IBinomialLadderFilter, DistributedBinomialLadderFilterClient>(x => dblfClient);

                
            }
            else
            {
                BinomialLadderFilter localPasswordBinomialLadderFilter =
                    new BinomialLadderFilter(options.NumberOfBitsInBinomialLadderFilter_N, options.HeightOfBinomialLadder_H);
                services.AddSingleton<IBinomialLadderFilter>(x => localPasswordBinomialLadderFilter);
            }

            LoginAttemptClient<MemoryUserAccount> loginAttemptClient = new LoginAttemptClient<MemoryUserAccount>(hosts, localHost);
            services.AddSingleton<ILoginAttemptClient, LoginAttemptClient<MemoryUserAccount>>(i => loginAttemptClient);
            services.AddSingleton<ILoginAttemptController, LoginAttemptController<MemoryUserAccount>>();
        }

        public void Configure(IApplicationBuilder app, IHostingEnvironment env, ILoggerFactory loggerFactory)
        {
            loggerFactory.AddConsole(Configuration.GetSection("Logging"));
            loggerFactory.AddDebug();

            app.UseMvc();
        }
    }
}
