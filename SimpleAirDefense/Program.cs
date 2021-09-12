using System;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using RurouniJones.SimpleAirDefense.Core;
using RurouniJones.SimpleAirDefense.Grpc;
using RurouniJones.SimpleAirDefense.Shared.Interfaces;
using Serilog;

namespace RurouniJones.SimpleAirDefense
{
    public class Program
    {
        public static void Main(string[] args)
        {
            if (OperatingSystem.IsWindows() && Environment.UserInteractive)
                ConsoleProperties.DisableQuickEdit();

            Environment.CurrentDirectory = AppDomain.CurrentDomain.BaseDirectory;

            var configuration = new ConfigurationBuilder()
                .AddYamlFile("configuration.yaml", false, true)
                .AddYamlFile("configuration.Development.yaml", true, true)
                .Build();

            Log.Logger = new LoggerConfiguration()
                .ReadFrom.Configuration(configuration)
                .CreateLogger();
            try
            {
                Log.Information("Starting SimpleAirDefense");
                CreateHostBuilder(args, configuration).Build().Run();
            }
            catch (Exception ex)
            {
                Log.Fatal(ex, "Could not start SimpleAirDefense");
            }
            finally
            {
                Log.Information("Stopping SimpleAirDefense");
                Log.CloseAndFlush();
            }
        }

        public static IHostBuilder CreateHostBuilder(string[] args, IConfigurationRoot config)
        {
            return Host.CreateDefaultBuilder(args)
                .ConfigureServices((hostContext, services) =>
                {
                    services.AddHostedService<Worker>();
                    services.AddSingleton<DefenderFactory>();
                    services.AddTransient<Defender>();
                    services.AddTransient<IRpcClient, RpcClient>();
                    services.Configure<Configuration>(config);
                    services.AddOptions();
                })
                .UseSerilog()
                .UseWindowsService();
        }

        // https://stackoverflow.com/questions/13656846/how-to-programmatic-disable-c-sharp-console-applications-quick-edit-mode
        internal static class ConsoleProperties
        {

            // STD_INPUT_HANDLE (DWORD): -10 is the standard input device.
            private const int StdInputHandle = -10;

            private const uint QuickEdit = 0x0040;

            [DllImport("kernel32.dll", SetLastError = true)]
            private static extern IntPtr GetStdHandle(int nStdHandle);

            [DllImport("kernel32.dll")]
            private static extern bool GetConsoleMode(IntPtr hConsoleHandle, out uint lpMode);

            [DllImport("kernel32.dll")]
            private static extern bool SetConsoleMode(IntPtr hConsoleHandle, uint dwMode);

            internal static bool DisableQuickEdit()
            {

                var consoleHandle = GetStdHandle(StdInputHandle);

                GetConsoleMode(consoleHandle, out var consoleMode);

                consoleMode &= ~QuickEdit;

                return SetConsoleMode(consoleHandle, consoleMode);
            }
        }
    }
}
