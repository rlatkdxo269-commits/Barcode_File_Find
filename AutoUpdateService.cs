using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Barcode_File_Find
{
    public sealed class AutoUpdateService
    {
        private const string LatestReleaseApiUrl = "https://api.github.com/repos/rlatkdxo269-commits/Barcode_File_Find/releases/latest";
        private const string InstallerAssetName = "Barcode_File_Find_Setup.exe";
        private static readonly HttpClient HttpClient = CreateHttpClient();

        public async Task CheckAndInstallUpdateAsync(Action<string> statusCallback, CancellationToken cancellationToken)
        {
            try
            {
                ReleaseInfo? latestRelease = await GetLatestReleaseAsync(cancellationToken);
                if (latestRelease == null || !TryParseVersion(latestRelease.TagName, out Version? latestVersion))
                {
                    return;
                }

                Version currentVersion = GetCurrentVersion();
                if (latestVersion <= currentVersion)
                {
                    Logger.Log($"업데이트 확인 완료: 현재 최신 버전입니다. ({currentVersion})");
                    return;
                }

                ReleaseAsset? installer = latestRelease.Assets
                    .FirstOrDefault(asset => asset.Name.Equals(InstallerAssetName, StringComparison.OrdinalIgnoreCase));
                if (installer == null || string.IsNullOrWhiteSpace(installer.DownloadUrl))
                {
                    Logger.Log($"업데이트 확인 실패: 최신 릴리스에서 {InstallerAssetName} 파일을 찾지 못했습니다.");
                    return;
                }

                statusCallback($"새 버전 {latestRelease.TagName}을(를) 다운로드하는 중입니다.");
                string installerPath = await DownloadInstallerAsync(installer.DownloadUrl, latestRelease.TagName, cancellationToken);

                Logger.Log($"새 버전 {latestRelease.TagName} 설치 파일 다운로드 완료: {installerPath}");
                statusCallback($"새 버전 {latestRelease.TagName} 설치를 시작합니다.");

                Process.Start(new ProcessStartInfo
                {
                    FileName = installerPath,
                    Arguments = "/Q",
                    UseShellExecute = true
                });

                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    System.Windows.Application.Current.Shutdown();
                });
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                Logger.Log($"업데이트 확인 중 오류 발생: {ex.Message}");
            }
        }

        private static async Task<ReleaseInfo?> GetLatestReleaseAsync(CancellationToken cancellationToken)
        {
            using HttpResponseMessage response = await HttpClient.GetAsync(LatestReleaseApiUrl, cancellationToken);
            response.EnsureSuccessStatusCode();

            await using Stream stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using JsonDocument document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
            JsonElement root = document.RootElement;

            string tagName = root.TryGetProperty("tag_name", out JsonElement tagElement)
                ? tagElement.GetString() ?? string.Empty
                : string.Empty;

            var assets = root.TryGetProperty("assets", out JsonElement assetsElement) && assetsElement.ValueKind == JsonValueKind.Array
                ? assetsElement.EnumerateArray()
                    .Select(asset => new ReleaseAsset(
                        asset.TryGetProperty("name", out JsonElement nameElement) ? nameElement.GetString() ?? string.Empty : string.Empty,
                        asset.TryGetProperty("browser_download_url", out JsonElement urlElement) ? urlElement.GetString() ?? string.Empty : string.Empty))
                    .ToArray()
                : Array.Empty<ReleaseAsset>();

            return string.IsNullOrWhiteSpace(tagName)
                ? null
                : new ReleaseInfo(tagName, assets);
        }

        private static async Task<string> DownloadInstallerAsync(string downloadUrl, string tagName, CancellationToken cancellationToken)
        {
            string updateDir = Path.Combine(Path.GetTempPath(), "Barcode_File_Find_Update", SanitizePathPart(tagName));
            Directory.CreateDirectory(updateDir);

            string installerPath = Path.Combine(updateDir, InstallerAssetName);
            using HttpResponseMessage response = await HttpClient.GetAsync(downloadUrl, cancellationToken);
            response.EnsureSuccessStatusCode();

            await using Stream remoteStream = await response.Content.ReadAsStreamAsync(cancellationToken);
            await using FileStream localStream = File.Create(installerPath);
            await remoteStream.CopyToAsync(localStream, cancellationToken);

            return installerPath;
        }

        private static Version GetCurrentVersion()
        {
            Version? version = Assembly.GetExecutingAssembly().GetName().Version;
            return version == null
                ? new Version(0, 0, 0)
                : new Version(version.Major, version.Minor, version.Build < 0 ? 0 : version.Build);
        }

        private static bool TryParseVersion(string tagName, out Version? version)
        {
            string normalized = tagName.Trim();
            if (normalized.StartsWith("v", StringComparison.OrdinalIgnoreCase))
            {
                normalized = normalized[1..];
            }

            int suffixIndex = normalized.IndexOfAny(['-', '+']);
            if (suffixIndex >= 0)
            {
                normalized = normalized[..suffixIndex];
            }

            string[] parts = normalized.Split('.', StringSplitOptions.RemoveEmptyEntries);
            normalized = parts.Length switch
            {
                1 => $"{parts[0]}.0.0",
                2 => $"{parts[0]}.{parts[1]}.0",
                _ => normalized
            };

            bool parsed = Version.TryParse(normalized, out Version? parsedVersion);
            version = parsedVersion;
            return parsed;
        }

        private static string SanitizePathPart(string value)
        {
            char[] invalidChars = Path.GetInvalidFileNameChars();
            return new string(value.Select(ch => invalidChars.Contains(ch) ? '_' : ch).ToArray());
        }

        private static HttpClient CreateHttpClient()
        {
            var client = new HttpClient();
            client.DefaultRequestHeaders.UserAgent.ParseAdd("Barcode-File-Find-AutoUpdater/1.0");
            client.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
            return client;
        }

        private sealed record ReleaseInfo(string TagName, ReleaseAsset[] Assets);
        private sealed record ReleaseAsset(string Name, string DownloadUrl);
    }
}
