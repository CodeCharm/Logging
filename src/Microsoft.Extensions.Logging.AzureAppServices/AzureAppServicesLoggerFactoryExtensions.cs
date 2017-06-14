﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.AzureAppServices;
using Microsoft.Extensions.Logging.AzureAppServices.Internal;
using Microsoft.Extensions.Options;

namespace Microsoft.Extensions.Logging
{
    /// <summary>
    /// Extension methods for adding Azure diagnostics logger.
    /// </summary>
    public static class AzureAppServicesLoggerFactoryExtensions
    {
        /// <summary>
        /// Adds an Azure Web Apps diagnostics logger.
        /// </summary>
        /// <param name="builder">The extension method argument</param>
        public static ILoggingBuilder AddAzureWebAppDiagnostics(this ILoggingBuilder builder)
        {
            var context = WebAppContext.Default;
            if (context.IsRunningInAzureWebApp)
            {
                var config = SiteConfigurationProvider.GetAzureLoggingConfiguration(context);

                builder.Services.AddSingleton<IConfigureOptions<LoggerFilterOptions>>(CreateFileFilterConfigureOptions(config));
                builder.Services.AddSingleton<IConfigureOptions<LoggerFilterOptions>>(CreateBlobFilterConfigureOptions(config));

                builder.Services.AddSingleton<IOptionsChangeTokenSource<LoggerFilterOptions>>(
                    new ConfigurationChangeTokenSource<LoggerFilterOptions>(config));

                builder.Services.AddSingleton<IConfigureOptions<AzureBlobLoggerOptions>>(new BlobLoggerConfigureOptions(config, context));
                builder.Services.AddSingleton<IOptionsChangeTokenSource<AzureBlobLoggerOptions>>(
                    new ConfigurationChangeTokenSource<AzureBlobLoggerOptions>(config));

                builder.Services.AddSingleton<IConfigureOptions<FileLoggerOptions>>(new FileLoggerConfigureOptions(config, context));
                builder.Services.AddSingleton<IOptionsChangeTokenSource<FileLoggerOptions>>(
                    new ConfigurationChangeTokenSource<FileLoggerOptions>(config));

                builder.Services.AddSingleton<IWebAppContext>(context);

                // Only add the provider if we're in Azure WebApp. That cannot change once the apps started
                builder.Services.AddSingleton<ILoggerProvider, FileLoggerProvider>();
                builder.Services.AddSingleton<ILoggerProvider, BlobLoggerProvider>();
            }

            return builder;
        }

        private static ConfigurationBasedLevelSwitcher CreateBlobFilterConfigureOptions(IConfiguration config)
        {
            return new ConfigurationBasedLevelSwitcher(
                configuration: config,
                provider: typeof(BlobLoggerProvider),
                levelKey: "AzureBlobTraceLevel");
        }

        private static ConfigurationBasedLevelSwitcher CreateFileFilterConfigureOptions(IConfiguration config)
        {
            return new ConfigurationBasedLevelSwitcher(
                configuration: config,
                provider: typeof(FileLoggerProvider),
                levelKey: "AzureDriveTraceLevel");
        }

        /// <summary>
        /// Adds an Azure Web Apps diagnostics logger.
        /// </summary>
        /// <param name="factory">The extension method argument</param>
        public static ILoggerFactory AddAzureWebAppDiagnostics(this ILoggerFactory factory)
        {
            return AddAzureWebAppDiagnostics(factory, new AzureAppServicesDiagnosticsSettings());
        }

        /// <summary>
        /// Adds an Azure Web Apps diagnostics logger.
        /// </summary>
        /// <param name="factory">The extension method argument</param>
        /// <param name="settings">The setting object to configure loggers.</param>
        public static ILoggerFactory AddAzureWebAppDiagnostics(this ILoggerFactory factory, AzureAppServicesDiagnosticsSettings settings)
        {
            var context = WebAppContext.Default;
            if (context.IsRunningInAzureWebApp)
            {
                var config = SiteConfigurationProvider.GetAzureLoggingConfiguration(context);

                // Only add the provider if we're in Azure WebApp. That cannot change once the apps started
                var fileOptions = new OptionsMonitor<FileLoggerOptions>(
                    new IConfigureOptions<FileLoggerOptions>[]
                    {
                        new FileLoggerConfigureOptions(config, context),
                        new ConfigureOptions<FileLoggerOptions>(options =>
                        {
                            options.FileSizeLimit = settings.FileSizeLimit;
                            options.RetainedFileCountLimit = settings.RetainedFileCountLimit;
                            options.BackgroundQueueSize = settings.BackgroundQueueSize;
                            if (settings.FileFlushPeriod != null)
                            {
                                options.FlushPeriod = settings.FileFlushPeriod.Value;
                            }
                        })
                    },
                    new []
                    {
                        new ConfigurationChangeTokenSource<FileLoggerOptions>(config)
                    }
                    );

                var blobOptions = new OptionsMonitor<AzureBlobLoggerOptions>(
                    new IConfigureOptions<AzureBlobLoggerOptions>[] {
                        new BlobLoggerConfigureOptions(config, context),
                        new ConfigureOptions<AzureBlobLoggerOptions>(options =>
                        {
                            options.BlobName = settings.BlobName;
                            options.BackgroundQueueSize = settings.BackgroundQueueSize;
                            options.FlushPeriod = settings.BlobCommitPeriod;
                            options.BatchSize = settings.BlobBatchSize;
                        })
                    },
                    new[]
                    {
                        new ConfigurationChangeTokenSource<AzureBlobLoggerOptions>(config)
                    }
                    );

                var filterOptions = new OptionsMonitor<LoggerFilterOptions>(
                    new []
                    {
                        CreateFileFilterConfigureOptions(config),
                        CreateBlobFilterConfigureOptions(config)
                    },
                    new [] { new ConfigurationChangeTokenSource<LoggerFilterOptions>(config) });

                factory.AddProvider(new ForwardingLoggerProvider(
                    new LoggerFactory(
                        new ILoggerProvider[]
                        {
                            new FileLoggerProvider(fileOptions),
                            new BlobLoggerProvider(blobOptions)
                        },
                        filterOptions
                        )
                    ));
            }
            return factory;
        }
    }
}
