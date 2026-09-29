using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Hydra.Vitals.PlayApi
{
    public sealed record StoreLanguageDiff(
        string Language,
        string? CurrentVideo,
        string? TargetVideo,
        bool VideoWillUpdate,
        int PhoneScreenshotCount,
        int SevenInchScreenshotCount,
        int TenInchScreenshotCount,
        bool ImagesWillClear);

    public sealed record StoreSyncReport(
        string PackageName,
        string DefaultLanguage,
        string? TargetVideoUrl,
        IReadOnlyList<StoreLanguageDiff> Diffs,
        bool IsDryRun,
        bool Committed);

    public interface IPlayStoreSyncService
    {
        Task<StoreSyncReport> AnalyzeSyncAsync(
            string packageName,
            string? preferredDefaultLanguage = null,
            CancellationToken ct = default);

        Task<StoreSyncReport> ExecuteSyncAsync(
            string packageName,
            string? preferredDefaultLanguage = null,
            CancellationToken ct = default);
    }

    public sealed class PlayStoreSyncService : IPlayStoreSyncService
    {
        private readonly IPlayPublisherClient _client;

        public PlayStoreSyncService(IPlayPublisherClient client)
        {
            _client = client;
        }

        public Task<StoreSyncReport> AnalyzeSyncAsync(
            string packageName,
            string? preferredDefaultLanguage = null,
            CancellationToken ct = default)
        {
            return RunSyncPipelineAsync(packageName, preferredDefaultLanguage, dryRun: true, ct);
        }

        public Task<StoreSyncReport> ExecuteSyncAsync(
            string packageName,
            string? preferredDefaultLanguage = null,
            CancellationToken ct = default)
        {
            return RunSyncPipelineAsync(packageName, preferredDefaultLanguage, dryRun: false, ct);
        }

        private async Task<StoreSyncReport> RunSyncPipelineAsync(
            string packageName,
            string? preferredDefaultLanguage,
            bool dryRun,
            CancellationToken ct)
        {
            string editId = await _client.InsertEditAsync(packageName, ct);
            try
            {
                var listings = await _client.GetListingsAsync(packageName, editId, ct);
                if (listings.Count == 0)
                {
                    throw new InvalidOperationException($"'{packageName}' icin yayinlanmis magaza listelemesi bulunamadi.");
                }

                // Varsayilan dili bul (en-US, en-GB, en veya ilk dil)
                var defaultListing = FindDefaultListing(listings, preferredDefaultLanguage);
                string targetVideo = defaultListing.Video ?? string.Empty;

                var diffs = new List<StoreLanguageDiff>();

                foreach (var listing in listings)
                {
                    // Varsayilan dilin kendisini degistirmiyoruz
                    if (string.Equals(listing.Language, defaultListing.Language, StringComparison.OrdinalIgnoreCase))
                        continue;

                    bool videoNeedsUpdate = !string.IsNullOrWhiteSpace(targetVideo) &&
                                            !string.Equals(listing.Video, targetVideo, StringComparison.Ordinal);

                    // Bu dil icin yuklenmis ozel ekran goruntulerini kontrol et
                    var phoneImages = await _client.GetImagesAsync(packageName, editId, listing.Language, "phoneScreenshots", ct);
                    var sevenInchImages = await _client.GetImagesAsync(packageName, editId, listing.Language, "sevenInchScreenshots", ct);
                    var tenInchImages = await _client.GetImagesAsync(packageName, editId, listing.Language, "tenInchScreenshots", ct);

                    bool hasImagesToDelete = phoneImages.Count > 0 || sevenInchImages.Count > 0 || tenInchImages.Count > 0;

                    diffs.Add(new StoreLanguageDiff(
                        Language: listing.Language,
                        CurrentVideo: listing.Video,
                        TargetVideo: targetVideo,
                        VideoWillUpdate: videoNeedsUpdate,
                        PhoneScreenshotCount: phoneImages.Count,
                        SevenInchScreenshotCount: sevenInchImages.Count,
                        TenInchScreenshotCount: tenInchImages.Count,
                        ImagesWillClear: hasImagesToDelete
                    ));

                    // Canli calistiriliyorsa degisiklikleri edit icine uygula
                    if (!dryRun)
                    {
                        if (videoNeedsUpdate)
                        {
                            await _client.UpdateListingVideoAsync(packageName, editId, listing.Language, targetVideo, ct);
                        }

                        if (phoneImages.Count > 0)
                        {
                            await _client.DeleteAllImagesAsync(packageName, editId, listing.Language, "phoneScreenshots", ct);
                        }

                        if (sevenInchImages.Count > 0)
                        {
                            await _client.DeleteAllImagesAsync(packageName, editId, listing.Language, "sevenInchScreenshots", ct);
                        }

                        if (tenInchImages.Count > 0)
                        {
                            await _client.DeleteAllImagesAsync(packageName, editId, listing.Language, "tenInchScreenshots", ct);
                        }
                    }
                }

                if (!dryRun)
                {
                    await _client.CommitEditAsync(packageName, editId, ct);
                    return new StoreSyncReport(packageName, defaultListing.Language, targetVideo, diffs, IsDryRun: false, Committed: true);
                }
                else
                {
                    await _client.DeleteEditAsync(packageName, editId, ct);
                    return new StoreSyncReport(packageName, defaultListing.Language, targetVideo, diffs, IsDryRun: true, Committed: false);
                }
            }
            catch
            {
                // Bir hata olursa askida edit oturumu kalmasin
                await _client.DeleteEditAsync(packageName, editId, CancellationToken.None);
                throw;
            }
        }

        private static AppListingDto FindDefaultListing(
            IReadOnlyList<AppListingDto> listings, string? preferred)
        {
            if (!string.IsNullOrWhiteSpace(preferred))
            {
                var match = listings.FirstOrDefault(l => l.Language.Equals(preferred, StringComparison.OrdinalIgnoreCase));
                if (match != null) return match;
            }

            // en-US, en-GB, en veya ilk
            return listings.FirstOrDefault(l => l.Language.Equals("en-US", StringComparison.OrdinalIgnoreCase))
                ?? listings.FirstOrDefault(l => l.Language.Equals("en-GB", StringComparison.OrdinalIgnoreCase))
                ?? listings.FirstOrDefault(l => l.Language.StartsWith("en", StringComparison.OrdinalIgnoreCase))
                ?? listings[0];
        }
    }
}
