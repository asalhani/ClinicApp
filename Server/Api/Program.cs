using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Entities.Appsettings;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Serilog;

namespace ClinicApp.Api
{
    
    public class Program
    {
        private static InternalSerilogExceptionsLogger _internalSerilogExceptionsLogger = null;

        public static void Main(string[] args)
        {
            // CreateHostBuilder(args).Build().Run();
            SetupService<Startup>(args, BuildConfiguration());
        }
        
        public static IConfiguration BuildConfiguration()
        {
            var currentEnv = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");

            return new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile($"appsettings.json", false)
                .AddJsonFile($"appsettings.{currentEnv}.json", optional: true)
                .AddEnvironmentVariables()
                .Build();
        }

        public static void SetupService<startup>(string[] args, IConfiguration configuration) where startup : class
        {
            var host = WebHost.CreateDefaultBuilder(args)
                .UseConfiguration(configuration)
                .UseStartup<startup>()
                .UseSerilog()
                .Build();

            SetupLogger(configuration);
            host.Run();
        }
        public static IWebHostBuilder CreateWebHostBuilder(string[] args) => WebHost.CreateDefaultBuilder(args)
            .UseSerilog((hostingContext, loggerConfiguration) => 
                loggerConfiguration
                    .ReadFrom.Configuration(hostingContext.Configuration)
                    .Enrich.FromLogContext()
                    .WriteTo.Console(
                        outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}")
            )
            .UseStartup<Startup>();
        
        public static void SetupLogger(IConfiguration configuration)
        {
            var maxResturtureDepth = configuration.GetSection("Serilog:MaxResturtureDepth").Get<int>();
            if( maxResturtureDepth == 0)
            {
                maxResturtureDepth = 10;        // fallback value
            }

            Log.Logger = new LoggerConfiguration()
                .Destructure.ToMaximumDepth(maxResturtureDepth)
                .ReadFrom.Configuration(configuration)
                .Enrich.FromLogContext()
                .CreateLogger();

            try
            {
                var internalSerilogConfigurationSettings = configuration.GetSection("Serilog:Internal").Get<InternalSerilogConfig>();
                if (internalSerilogConfigurationSettings.Enable)
                {
                    _internalSerilogExceptionsLogger = new InternalSerilogExceptionsLogger(internalSerilogConfigurationSettings);
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "missing Serilog internal settings. Serilog will fail silently (not recommended).");
            }
        }
    }

    sealed class InternalSerilogExceptionsLogger
        {
            private StreamWriter _streamWriter = null;
            private TextWriter _threadSafeWriter = null;
            private InternalSerilogConfig _internalSerilogConfig = null;
            private string _directoryName;
            private string _fileNameWithoutExtension;
            private string _extension;
            private readonly float _rotateSizeBytes;

            public InternalSerilogExceptionsLogger(InternalSerilogConfig configuration)
            {
                try
                {
                    _directoryName = Path.GetDirectoryName(configuration.SerilogExceptionsFile);
                    _fileNameWithoutExtension = Path.GetFileNameWithoutExtension(configuration.SerilogExceptionsFile);
                    _extension = Path.GetExtension(configuration.SerilogExceptionsFile);

                    _internalSerilogConfig = configuration;
                    _rotateSizeBytes = _internalSerilogConfig.RotateSizeMB * 1024 * 1024;
                    Setup();
                    Thread.Sleep(_internalSerilogConfig.HeartBeatMilliSeconds);
                    Task.Run(RotateIfNeeded);
                }
                catch (Exception ex)
                {
                    Serilog.Debugging.SelfLog.Disable();
                    Log.Error(ex, "failed to initialize internal serilog exception logger");
                }
            }

            private void Setup()
            {
                _streamWriter = File.CreateText(_internalSerilogConfig.SerilogExceptionsFile);
                _streamWriter.AutoFlush = true;
                _threadSafeWriter = TextWriter.Synchronized(_streamWriter);
                Serilog.Debugging.SelfLog.Enable(LogInternalSerilogException);
            }

            private async Task RotateIfNeeded()
            {
                await Task.Run(() =>
                {
                    if (_streamWriter.BaseStream.Length >= _rotateSizeBytes)
                    {
                        Rotate();
                        Setup();
                    }
                    Thread.Sleep(_internalSerilogConfig.HeartBeatMilliSeconds);
                    Task.Run(RotateIfNeeded);
                });
            }

            private void Rotate()
            {
                Serilog.Debugging.SelfLog.Disable();        // This important to prevent other threads from using _threadSafeWriter
                _threadSafeWriter.Flush();
                _threadSafeWriter.Close();
                var rotatedFileFullName = $"{_directoryName}/{_fileNameWithoutExtension}_{DateTime.Now:dd_MM_yy_h_m_s}{_extension}";
                File.Move(_internalSerilogConfig.SerilogExceptionsFile, rotatedFileFullName);
            }

            private void LogInternalSerilogException(string message)
            {
                _threadSafeWriter.WriteLine("==== exception start =======");
                _threadSafeWriter.WriteLine($"environment: {Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")}");
                _threadSafeWriter.WriteLine($"reporter machine: {Environment.MachineName}");
                _threadSafeWriter.WriteLine($"message:\n{message}\n");
                _threadSafeWriter.WriteLine($"stacktrace:\n{Environment.StackTrace}");
                _threadSafeWriter.WriteLine("==== exception end =======\n");
                _threadSafeWriter.Flush();
            }
        }
}
