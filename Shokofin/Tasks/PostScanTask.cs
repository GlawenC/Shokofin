using System;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Controller.Library;
using Shokofin.API;
using Shokofin.Collections;
using Shokofin.MergeVersions;
using Shokofin.Resolvers;

namespace Shokofin.Tasks;

public class PostScanTask : ILibraryPostScanTask
{
    private readonly MergeVersionsManager VersionsManager;

    private readonly CollectionManager CollectionManager;

    public PostScanTask(MergeVersionsManager versionsManager, CollectionManager collectionManager)
    {
        VersionsManager = versionsManager;
        CollectionManager = collectionManager;
    }

    public async Task Run(IProgress<double> progress, CancellationToken token)
    {
        // Merge versions now if the setting is enabled.
        if (Plugin.Instance.Configuration.AutoMergeVersions) {
            // Setup basic progress tracking
            var baseProgress = 0d;
            var simpleProgress = new Progress<double>(value => progress.Report(baseProgress + (value / 2d)));

            // Merge versions.
            await VersionsManager.MergeAll(simpleProgress, token);

            // Reconstruct collections.
            baseProgress = 50;
            await CollectionManager.ReconstructCollections(simpleProgress, token);

            progress.Report(100d);
        }
        else {
            // Reconstruct collections.
            await CollectionManager.ReconstructCollections(progress, token);
        }
    }
}
