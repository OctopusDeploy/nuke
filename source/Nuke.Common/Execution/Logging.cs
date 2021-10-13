// Copyright 2021 Maintainers of NUKE.
// Distributed under the MIT License.
// https://github.com/nuke-build/nuke/blob/master/LICENSE

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using JetBrains.Annotations;
using Nuke.Common.CI;
using Nuke.Common.IO;
using Nuke.Common.Utilities;
using Nuke.Common.Utilities.Collections;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using Serilog.Sinks.SystemConsole.Themes;

namespace Nuke.Common.Execution
{
    public static partial class Logging
    {
        private static readonly LoggingLevelSwitch LevelSwitch = new LoggingLevelSwitch();

        internal static LogLevel Level
        {
            get => LevelSwitch.MinimumLevel switch
            {
                LogEventLevel.Verbose => LogLevel.Trace,
                LogEventLevel.Debug => LogLevel.Normal,
                LogEventLevel.Warning => LogLevel.Warning,
                LogEventLevel.Error => LogLevel.Error,
                _ => throw new ArgumentOutOfRangeException(nameof(LevelSwitch), LevelSwitch.MinimumLevel, message: null)
            };
            set => LevelSwitch.MinimumLevel = value switch
            {
                LogLevel.Trace => LogEventLevel.Verbose,
                LogLevel.Normal => LogEventLevel.Debug,
                LogLevel.Warning => LogEventLevel.Warning,
                LogLevel.Error => LogEventLevel.Error,
                _ => throw new ArgumentOutOfRangeException(nameof(value), value, message: null)
            };
        }

        public static void Configure(Func<LoggerConfiguration, LoggerConfiguration> configurationFactory)
        {
            Log.Logger = configurationFactory.Invoke(new LoggerConfiguration()).CreateLogger();
        }

        public static LoggerConfiguration ConfigureBuild(this LoggerConfiguration configuration, NukeBuild build)
        {
            return configuration
                .ConfigureHost()
                .ConfigureConsole(build)
                .ConfigureInMemory()
                .ConfigureFiles(build)
                .MinimumLevel.Is(LogEventLevel.Debug);
        }

        internal static LoggerConfiguration ConfigureConsole(this LoggerConfiguration configuration, NukeBuild build = null)
        {
            return configuration
                .WriteTo.Console(
                    outputTemplate: build != null ? NukeBuild.Host.OutputTemplate : Host.DefaultOutputTemplate,
                    theme: (ConsoleTheme)(build != null ? NukeBuild.Host.Theme : Host.DefaultTheme),
                    applyThemeToRedirectedOutput: true,
                    levelSwitch: LevelSwitch);
        }

        private static LoggerConfiguration ConfigureHost(this LoggerConfiguration configuration)
        {
            return configuration
                .WriteTo.Sink<Host.LogEventSink>(restrictedToMinimumLevel: LogEventLevel.Warning);
        }

        private static LoggerConfiguration ConfigureInMemory(this LoggerConfiguration configuration, LogEventLevel minimumLevel = LogEventLevel.Warning)
        {
            return configuration
                .WriteTo.Sink(InMemorySink.Instance, minimumLevel);
        }

        private static LoggerConfiguration ConfigureFiles(this LoggerConfiguration configuration, NukeBuild build)
        {
            if (NukeBuild.Host is IBuildServer)
                return configuration;

            DeleteOldLogFiles();

            var buildLogFile = NukeBuild.TemporaryDirectory / "build.log";
            var targetPadding = build.TargetNames.Max(x => x.Length);
            return configuration
                .Enrich.With<TargetLogEventEnricher>()
                .WriteTo.File(
                    path: buildLogFile,
                    outputTemplate: $"{{Timestamp:HH:mm:ss.fff}} | {{Level:u1}} | {{Target,{targetPadding}}} | {{Message:l}}{{NewLine}}{{Exception}}")
                .WriteTo.File(
                    path: Path.ChangeExtension(buildLogFile, $".{DateTime.Now:s}.log"),
                    outputTemplate: $"{{Level:u1}} | {{Target,{targetPadding}}} | {{Message:l}}{{NewLine}}{{Exception}}");
        }

        private static void DeleteOldLogFiles()
        {
            var configurationId = EnvironmentInfo.GetParameter<string>(BuildServerConfigurationGenerationAttributeBase.ConfigurationParameterName);
            if (configurationId != null)
                return;

            NukeBuild.TemporaryDirectory.GlobFiles("build.*.log").OrderByDescending(x => x.ToString()).Skip(5)
                .ForEach(FileSystemTasks.DeleteFile);

            var buildLogFile = NukeBuild.TemporaryDirectory / "build.log";
            if (File.Exists(buildLogFile))
            {
                using var filestream = File.OpenWrite(buildLogFile);
                filestream.SetLength(0);
            }
        }

        internal static IDisposable SetTarget(string name)
        {
            return DelegateDisposable.CreateBracket(
                () => TargetLogEventEnricher.Property = new LogEventProperty("Target", new ScalarValue(name)),
                () => TargetLogEventEnricher.Property = null);
        }

        internal class InMemorySink : ILogEventSink, IDisposable
        {
            public static InMemorySink Instance { get; } = new InMemorySink();

            private readonly List<LogEvent> _logEvents;

            private InMemorySink()
            {
                _logEvents = new List<LogEvent>();
            }

            public IReadOnlyCollection<LogEvent> LogEvents => _logEvents.AsReadOnly();

            public void Emit(LogEvent logEvent)
            {
                logEvent.AddOrUpdateProperty(TargetLogEventEnricher.Current);
                _logEvents.Add(logEvent);
            }

            public void Dispose()
            {
                _logEvents.Clear();
            }
        }

        internal class TargetLogEventEnricher : ILogEventEnricher
        {
            private static LogEventProperty s_defaultProperty = new LogEventProperty("Target", new ScalarValue(""));

            public static LogEventProperty Property;

            public static LogEventProperty Current => Property ?? s_defaultProperty;

            public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
            {
                logEvent.AddOrUpdateProperty(Current);
            }
        }
    }
}
