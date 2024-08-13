using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Net;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Controller.Configuration;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Serialization;
using Microsoft.Extensions.Logging;
using Shokofin.Configuration;
using Shokofin.Utils;

namespace Shokofin;

public class Plugin : BasePlugin<PluginConfiguration>, IHasWebPages
{
    private readonly IServerConfigurationManager ConfigurationManager;

    public const string MetadataProviderName = "Shoko";

    public override string Name => MetadataProviderName;

    public override Guid Id => Guid.Parse("5216ccbf-d24a-4eb3-8a7e-7da4230b7052");

    /// <summary>
    /// Indicates that we can create symbolic links.
    /// </summary>
    public readonly bool CanCreateSymbolicLinks;

    /// <summary>
    /// Usage tracker for automagically clearing the caches when nothing is using them.
    /// </summary>
    public readonly UsageTracker Tracker;

    private readonly ILogger<Plugin> Logger;

    /// <summary>
    /// "Virtual" File System Root Directory.
    /// </summary>
    public readonly string VirtualRoot;

    /// <summary>
    /// Base URL where the Jellyfin is running.
    /// </summary>
    public string BaseUrl => ConfigurationManager.GetNetworkConfiguration() is { } networkOptions
        ? $"{
            (networkOptions.RequireHttps && networkOptions.EnableHttps ? "https" : "http")
        }://{
            (networkOptions.LocalNetworkAddresses.FirstOrDefault() is { } address && address is not "0.0.0.0" ? address : "localhost")
        }:{
            (networkOptions.RequireHttps && networkOptions.EnableHttps ? networkOptions.InternalHttpsPort : networkOptions.InternalHttpPort)
        }/"
        : "http://localhost:8096/";

    /// <summary>
    /// Gets or sets the event handler that is triggered when this configuration changes.
    /// </summary>
    public new event EventHandler<PluginConfiguration>? ConfigurationChanged;

    public Plugin(ILoggerFactory loggerFactory, IServerConfigurationManager configurationManager, IApplicationPaths applicationPaths, IXmlSerializer xmlSerializer, ILogger<Plugin> logger) : base(applicationPaths, xmlSerializer)
    {
        ConfigurationManager = configurationManager;
        Instance = this;
        base.ConfigurationChanged += OnConfigChanged;
        VirtualRoot = Path.Join(applicationPaths.ProgramDataPath, "Shokofin", "VFS");
        Tracker = new(loggerFactory.CreateLogger<UsageTracker>(), TimeSpan.FromSeconds(60));
        Logger = logger;
        CanCreateSymbolicLinks = true;
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) {
            var target = Path.Join(Path.GetDirectoryName(VirtualRoot)!, "TestTarget.txt");
            var link = Path.Join(Path.GetDirectoryName(VirtualRoot)!, "TestLink.txt");
            try {
                if (!Directory.Exists(Path.GetDirectoryName(VirtualRoot)!))
                    Directory.CreateDirectory(Path.GetDirectoryName(VirtualRoot)!);
                File.WriteAllText(target, string.Empty);
                File.CreateSymbolicLink(link, target);
            }
            catch {
                CanCreateSymbolicLinks = false;
            }
            finally {
                if (File.Exists(link))
                    File.Delete(link);
                if (File.Exists(target))
                    File.Delete(target);
            }
        }
        IgnoredFolders = Configuration.IgnoredFolders.ToHashSet();
        Tracker.UpdateTimeout(TimeSpan.FromSeconds(Configuration.UsageTracker_StalledTimeInSeconds));
        Logger.LogDebug("Virtual File System Location; {Path}", VirtualRoot);
        Logger.LogDebug("Can create symbolic links; {Value}", CanCreateSymbolicLinks);
    }

    public void UpdateConfiguration()
    {
        UpdateConfiguration(this.Configuration);
    }

    public void OnConfigChanged(object? sender, BasePluginConfiguration e)
    {
        if (e is not PluginConfiguration config)
            return;
        IgnoredFolders = config.IgnoredFolders.ToHashSet();
        Tracker.UpdateTimeout(TimeSpan.FromSeconds(Configuration.UsageTracker_StalledTimeInSeconds));
        ConfigurationChanged?.Invoke(sender, config);
    }

    public HashSet<string> IgnoredFolders;

#pragma warning disable 8618
    public static Plugin Instance { get; private set; }
#pragma warning restore 8618

    public IEnumerable<PluginPageInfo> GetPages()
    {
        return new[]
        {
            new PluginPageInfo
            {
                Name = Name,
                EmbeddedResourcePath = $"{GetType().Namespace}.Configuration.configPage.html",
            },
            new PluginPageInfo
            {
                Name = "ShokoController.js",
                EmbeddedResourcePath = $"{GetType().Namespace}.Configuration.configController.js",
            },
        };
    }
}
