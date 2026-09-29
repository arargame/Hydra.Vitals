using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Hydra.Vitals.PlayApi
{
    public sealed record AppListingDto(
        string Language,
        string Title,
        string ShortDescription,
        string FullDescription,
        string? Video);

    public sealed record AppImageDto(
        string Id,
        string Url,
        string? Sha1,
        string? Sha256);

    public sealed record StoreListingDetails(
        string Language,
        string Title,
        string ShortDescription,
        string FullDescription,
        string? Video,
        IReadOnlyList<AppImageDto> Icons,
        IReadOnlyList<AppImageDto> FeatureGraphics,
        IReadOnlyList<AppImageDto> PhoneScreenshots,
        IReadOnlyList<AppImageDto> SevenInchScreenshots,
        IReadOnlyList<AppImageDto> TenInchScreenshots);

    public interface IPlayPublisherClient
    {
        Task<string> InsertEditAsync(string packageName, CancellationToken ct = default);
        Task<IReadOnlyList<AppListingDto>> GetListingsAsync(string packageName, string editId, CancellationToken ct = default);
        Task<AppListingDto?> GetListingAsync(string packageName, string editId, string language, CancellationToken ct = default);
        Task<StoreListingDetails?> GetFullListingDetailsAsync(string packageName, string editId, string language, CancellationToken ct = default);
        Task<AppListingDto> UpdateListingVideoAsync(string packageName, string editId, string language, string videoUrl, CancellationToken ct = default);
        Task<IReadOnlyList<AppImageDto>> GetImagesAsync(string packageName, string editId, string language, string imageType, CancellationToken ct = default);
        Task DeleteAllImagesAsync(string packageName, string editId, string language, string imageType, CancellationToken ct = default);
        Task DeleteImageAsync(string packageName, string editId, string language, string imageType, string imageId, CancellationToken ct = default);
        Task CommitEditAsync(string packageName, string editId, CancellationToken ct = default);
        Task DeleteEditAsync(string packageName, string editId, CancellationToken ct = default);
    }

    /// <summary>
    /// Google Play Android Publisher API (Edits API v3) istemcisi.
    /// Magaza listelemeleri, YouTube videolari ve ekran goruntulerini yonetir.
    /// </summary>
    public sealed class PlayPublisherClient : GoogleApiClientBase, IPlayPublisherClient
    {
        private const string BaseUrl = "https://androidpublisher.googleapis.com/androidpublisher/v3/applications";

        public PlayPublisherClient(IGoogleTokenService tokens, HttpClient http)
            : base(tokens, http)
        {
        }

        public async Task<string> InsertEditAsync(string packageName, CancellationToken ct = default)
        {
            var url = $"{BaseUrl}/{Uri.EscapeDataString(packageName)}/edits";
            using var doc = await SendAsync(HttpMethod.Post, url, "{}", ct);
            return doc.RootElement.GetProperty("id").GetString()
                ?? throw new InvalidOperationException("Google Play edit ID dondurmedi.");
        }

        public async Task<IReadOnlyList<AppListingDto>> GetListingsAsync(
            string packageName, string editId, CancellationToken ct = default)
        {
            var url = $"{BaseUrl}/{Uri.EscapeDataString(packageName)}/edits/{Uri.EscapeDataString(editId)}/listings";
            using var doc = await SendAsync(HttpMethod.Get, url, null, ct);

            var list = new List<AppListingDto>();
            if (!doc.RootElement.TryGetProperty("listings", out var array) || array.ValueKind != JsonValueKind.Array)
                return list;

            foreach (var item in array.EnumerateArray())
            {
                list.Add(ParseListing(item));
            }

            return list;
        }

        public async Task<AppListingDto?> GetListingAsync(
            string packageName, string editId, string language, CancellationToken ct = default)
        {
            var url = $"{BaseUrl}/{Uri.EscapeDataString(packageName)}/edits/{Uri.EscapeDataString(editId)}/listings/{Uri.EscapeDataString(language)}";
            try
            {
                using var doc = await SendAsync(HttpMethod.Get, url, null, ct);
                return ParseListing(doc.RootElement);
            }
            catch (GoogleApiException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return null;
            }
        }

        public async Task<StoreListingDetails?> GetFullListingDetailsAsync(
            string packageName, string editId, string language, CancellationToken ct = default)
        {
            var listing = await GetListingAsync(packageName, editId, language, ct);
            if (listing == null) return null;

            var icons = await GetImagesAsync(packageName, editId, language, "icon", ct);
            var featureGraphics = await GetImagesAsync(packageName, editId, language, "featureGraphic", ct);
            var phoneScreenshots = await GetImagesAsync(packageName, editId, language, "phoneScreenshots", ct);
            var sevenInchScreenshots = await GetImagesAsync(packageName, editId, language, "sevenInchScreenshots", ct);
            var tenInchScreenshots = await GetImagesAsync(packageName, editId, language, "tenInchScreenshots", ct);

            return new StoreListingDetails(
                Language: listing.Language,
                Title: listing.Title,
                ShortDescription: listing.ShortDescription,
                FullDescription: listing.FullDescription,
                Video: listing.Video,
                Icons: icons,
                FeatureGraphics: featureGraphics,
                PhoneScreenshots: phoneScreenshots,
                SevenInchScreenshots: sevenInchScreenshots,
                TenInchScreenshots: tenInchScreenshots
            );
        }

        public async Task<AppListingDto> UpdateListingVideoAsync(
            string packageName, string editId, string language, string videoUrl, CancellationToken ct = default)
        {
            var url = $"{BaseUrl}/{Uri.EscapeDataString(packageName)}/edits/{Uri.EscapeDataString(editId)}/listings/{Uri.EscapeDataString(language)}";
            var body = JsonSerializer.Serialize(new { video = videoUrl });
            using var doc = await SendAsync(HttpMethod.Patch, url, body, ct);
            return ParseListing(doc.RootElement);
        }

        public async Task<IReadOnlyList<AppImageDto>> GetImagesAsync(
            string packageName, string editId, string language, string imageType, CancellationToken ct = default)
        {
            var url = $"{BaseUrl}/{Uri.EscapeDataString(packageName)}/edits/{Uri.EscapeDataString(editId)}/listings/{Uri.EscapeDataString(language)}/{Uri.EscapeDataString(imageType)}";
            try
            {
                using var doc = await SendAsync(HttpMethod.Get, url, null, ct);
                var list = new List<AppImageDto>();
                if (!doc.RootElement.TryGetProperty("images", out var array) || array.ValueKind != JsonValueKind.Array)
                    return list;

                foreach (var item in array.EnumerateArray())
                {
                    string id = item.TryGetProperty("id", out var pId) ? pId.GetString() ?? "" : "";
                    string imgUrl = item.TryGetProperty("url", out var pUrl) ? pUrl.GetString() ?? "" : "";
                    string? sha1 = item.TryGetProperty("sha1", out var pSha1) ? pSha1.GetString() : null;
                    string? sha256 = item.TryGetProperty("sha256", out var pSha256) ? pSha256.GetString() : null;
                    list.Add(new AppImageDto(id, imgUrl, sha1, sha256));
                }

                return list;
            }
            catch (GoogleApiException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return Array.Empty<AppImageDto>();
            }
        }

        public async Task DeleteAllImagesAsync(
            string packageName, string editId, string language, string imageType, CancellationToken ct = default)
        {
            var url = $"{BaseUrl}/{Uri.EscapeDataString(packageName)}/edits/{Uri.EscapeDataString(editId)}/listings/{Uri.EscapeDataString(language)}/{Uri.EscapeDataString(imageType)}";
            try
            {
                await SendAsync(HttpMethod.Delete, url, null, ct);
            }
            catch (GoogleApiException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                // Zaten resim yok.
            }
        }

        public async Task DeleteImageAsync(
            string packageName, string editId, string language, string imageType, string imageId, CancellationToken ct = default)
        {
            var url = $"{BaseUrl}/{Uri.EscapeDataString(packageName)}/edits/{Uri.EscapeDataString(editId)}/listings/{Uri.EscapeDataString(language)}/{Uri.EscapeDataString(imageType)}/{Uri.EscapeDataString(imageId)}";
            try
            {
                await SendAsync(HttpMethod.Delete, url, null, ct);
            }
            catch (GoogleApiException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                // Zaten silinmis.
            }
        }

        public async Task CommitEditAsync(string packageName, string editId, CancellationToken ct = default)
        {
            var url = $"{BaseUrl}/{Uri.EscapeDataString(packageName)}/edits/{Uri.EscapeDataString(editId)}:commit";
            await SendAsync(HttpMethod.Post, url, null, ct);
        }

        public async Task DeleteEditAsync(string packageName, string editId, CancellationToken ct = default)
        {
            var url = $"{BaseUrl}/{Uri.EscapeDataString(packageName)}/edits/{Uri.EscapeDataString(editId)}";
            try
            {
                await SendAsync(HttpMethod.Delete, url, null, ct);
            }
            catch
            {
                // Gecersizlestirme hatasi yoksayilabilir.
            }
        }

        private static AppListingDto ParseListing(JsonElement el)
        {
            string lang = el.TryGetProperty("language", out var l) ? l.GetString() ?? "" : "";
            string title = el.TryGetProperty("title", out var t) ? t.GetString() ?? "" : "";
            string sDesc = el.TryGetProperty("shortDescription", out var sd) ? sd.GetString() ?? "" : "";
            string fDesc = el.TryGetProperty("fullDescription", out var fd) ? fd.GetString() ?? "" : "";
            string? video = el.TryGetProperty("video", out var v) ? v.GetString() : null;

            return new AppListingDto(lang, title, sDesc, fDesc, video);
        }
    }
}
