using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;


namespace Starward.Features.Setting;

/// <summary>
/// WebDAV 服务器上的备份文件信息
/// </summary>
public class WebDAVFileInfo
{


    public string Name { get; set; } = "";


    public string Url { get; set; } = "";


    public long Size { get; set; }


    public DateTime LastModified { get; set; }


    public string SizeText => WebDAVClient.FormatFileSize(Size);


    public string LastModifiedText => LastModified == DateTime.MinValue ? "" : LastModified.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss");


}


/// <summary>
/// 基于 HttpClient 的 WebDAV 客户端，支持 MKCOL、PUT、PROPFIND、GET、DELETE 方法
/// </summary>
public static class WebDAVClient
{


    public const string BackupFolderName = "StarwardDatabaseBackup";


    public static string ServerAddress => AppConfig.GetValue<string>(null, "WebDAVServerAddress") ?? "";


    public static string? UserName => AppConfig.GetValue<string>(null, "WebDAVUserName");


    public static string? Password => AppConfig.GetValue<string>(null, "WebDAVPassword");


    public static bool IsConfigured => !string.IsNullOrWhiteSpace(ServerAddress)
        && Uri.TryCreate(ServerAddress, UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https";


    public static string GetBackupFolderUrl(string serverAddress) => serverAddress.TrimEnd('/') + "/" + BackupFolderName;


    /// <summary>
    /// 创建带 Basic 认证的 HttpClient
    /// </summary>
    public static HttpClient CreateHttpClient(string serverAddress, string? userName, string? password, TimeSpan? timeout = null)
    {
        var httpClient = new HttpClient(new SocketsHttpHandler
        {
            AutomaticDecompression = DecompressionMethods.All,
        })
        {
            Timeout = timeout ?? TimeSpan.FromSeconds(300),
        };
        httpClient.DefaultRequestHeaders.ExpectContinue = false;
#if DEBUG
        httpClient.DefaultRequestHeaders.Add("User-Agent", $"Starward.Debug/{AppConfig.AppVersion}");
#else
        httpClient.DefaultRequestHeaders.Add("User-Agent", $"Starward/{AppConfig.AppVersion}");
#endif
        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic",
            Convert.ToBase64String(Encoding.UTF8.GetBytes($"{userName ?? ""}:{password ?? ""}")));
        return httpClient;
    }


    public static string FormatFileSize(long bytes) => bytes < 1 << 20 ? $"{bytes / (double)(1 << 10):F1} KB" : $"{bytes / (double)(1 << 20):F1} MB";


    public static string FormatSpeed(double bytesPerSecond) => bytesPerSecond < 1 << 20 ? $"{bytesPerSecond / (1 << 10):F1} KB/s" : $"{bytesPerSecond / (1 << 20):F1} MB/s";


    /// <summary>
    /// 创建节流的进度回调：每累计传输 minIntervalBytes 字节触发一次，并计算百分比与速度
    /// </summary>
    public static Action<long> CreateThrottledProgress(long totalBytes, Stopwatch stopwatch, Action<double, double> onProgress, long minIntervalBytes = 256 * 1024)
    {
        long lastReportedBytes = 0;
        return uploadedBytes =>
        {
            // 最后不足 minIntervalBytes 的尾部数据也强制上报，避免进度停滞在接近 100%
            bool isFinal = totalBytes > 0 && uploadedBytes >= totalBytes;
            if (!isFinal && uploadedBytes - lastReportedBytes < minIntervalBytes)
            {
                return;
            }
            lastReportedBytes = uploadedBytes;
            double percent = totalBytes == 0 ? 100 : uploadedBytes * 100.0 / totalBytes;
            double speed = stopwatch.Elapsed.TotalSeconds > 0 ? uploadedBytes / stopwatch.Elapsed.TotalSeconds : 0;
            onProgress(percent, speed);
        };
    }


    /// <summary>
    /// 列出服务器上备份文件夹中的文件（PROPFIND）
    /// </summary>
    public static async Task<List<WebDAVFileInfo>> ListFilesAsync(string serverAddress, string? userName, string? password, CancellationToken cancellationToken = default)
    {
        using var httpClient = CreateHttpClient(serverAddress, userName, password);
        using var propfind = new HttpRequestMessage(new HttpMethod("PROPFIND"), GetBackupFolderUrl(serverAddress));
        propfind.Headers.Add("Depth", "1");
        using var response = await SendAsync(httpClient, propfind, cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            // 尚未创建备份文件夹时视为空列表
            return new List<WebDAVFileInfo>();
        }
        if (response.StatusCode != HttpStatusCode.MultiStatus)
        {
            throw CreateHttpException(Lang.WebDAVError_ListingFiles, response.StatusCode);
        }
        string xml = await response.Content.ReadAsStringAsync(cancellationToken);
        var files = ParseMultistatus(xml);
        // 部分服务器可能返回相对路径的 href，需基于备份文件夹 URL 补全为完整地址
        foreach (var file in files)
        {
            if (Uri.TryCreate(file.Url, UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https")
            {
                continue;
            }
            file.Url = GetBackupFolderUrl(serverAddress) + "/" + Uri.EscapeDataString(file.Name);
        }
        return files;
    }


    /// <summary>
    /// 解析 PROPFIND 返回的 Multi-Status XML
    /// </summary>
    private static List<WebDAVFileInfo> ParseMultistatus(string xml)
    {
        var list = new List<WebDAVFileInfo>();
        XNamespace dav = "DAV:";
        XDocument doc;
        try
        {
            doc = XDocument.Parse(xml);
        }
        catch (System.Xml.XmlException ex)
        {
            throw new HttpRequestException(Lang.WebDAVError_InvalidResponse, ex);
        }
        foreach (var response in doc.Descendants(dav + "response"))
        {
            string href = response.Element(dav + "href")?.Value ?? "";
            if (string.IsNullOrWhiteSpace(href) || href.EndsWith('/'))
            {
                continue;
            }
            var prop = response.Descendants(dav + "prop").FirstOrDefault();
            // 部分服务器返回的目录 href 可能不以 / 结尾，需检查 resourcetype
            if (prop?.Element(dav + "resourcetype")?.Element(dav + "collection") is not null)
            {
                continue;
            }
            string name = Path.GetFileName(Uri.UnescapeDataString(Path.GetFileName(Uri.TryCreate(href, UriKind.Absolute, out var uri) ? uri.AbsolutePath : href)));
            long size = long.TryParse(prop?.Element(dav + "getcontentlength")?.Value, out long length) ? length : 0;
            DateTime.TryParse(prop?.Element(dav + "getlastmodified")?.Value, CultureInfo.InvariantCulture,
                DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out DateTime lastModified);
            list.Add(new WebDAVFileInfo
            {
                Name = name,
                Url = href,
                Size = size,
                LastModified = lastModified,
            });
        }
        return list;
    }


    /// <summary>
    /// 上传文件到备份文件夹（MKCOL 创建文件夹，文件夹已存在则忽略）
    /// </summary>
    public static async Task UploadFileAsync(string serverAddress, string? userName, string? password, string file, Action<long>? onProgress = null, CancellationToken cancellationToken = default)
    {
        using var httpClient = CreateHttpClient(serverAddress, userName, password);
        string folderUrl = GetBackupFolderUrl(serverAddress);
        string fileUrl = folderUrl + "/" + Uri.EscapeDataString(Path.GetFileName(file));
        using (var mkcol = new HttpRequestMessage(new HttpMethod("MKCOL"), folderUrl))
        using (var response = await SendAsync(httpClient, mkcol, cancellationToken))
        {
            // 409 Conflict 表示父目录不存在，无法创建备份文件夹；405 Method Not Allowed 表示文件夹已存在
            if (response.StatusCode == HttpStatusCode.Conflict)
            {
                throw new HttpRequestException($"{Lang.WebDAVError_ParentFolderNotFound} (HTTP 409)");
            }
            if (response.StatusCode is not (HttpStatusCode.Created or HttpStatusCode.OK or HttpStatusCode.NoContent or HttpStatusCode.MethodNotAllowed))
            {
                throw CreateHttpException(Lang.WebDAVError_CreatingFolder, response.StatusCode);
            }
        }
        using var stream = File.OpenRead(file);
        Stream uploadStream = stream;
        if (onProgress is not null)
        {
            uploadStream = new ProgressStream(stream, stream.Length, onProgress);
        }
        using var content = new StreamContent(uploadStream);
        content.Headers.ContentLength = stream.Length;
        using var put = new HttpRequestMessage(HttpMethod.Put, fileUrl) { Content = content };
        using (var response = await SendAsync(httpClient, put, cancellationToken))
        {
            if (response.StatusCode is not (HttpStatusCode.Created or HttpStatusCode.OK or HttpStatusCode.NoContent))
            {
                throw CreateHttpException(Lang.WebDAVError_UploadingFile, response.StatusCode);
            }
        }
    }


    /// <summary>
    /// 下载文件到本地
    /// </summary>
    public static async Task DownloadFileAsync(string serverAddress, string? userName, string? password, string fileUrl, string localPath, Action<long>? onProgress = null, long? expectedLength = null, CancellationToken cancellationToken = default)
    {
        using var httpClient = CreateHttpClient(serverAddress, userName, password);
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, fileUrl);
            using var response = await SendAsync(httpClient, request, cancellationToken, HttpCompletionOption.ResponseHeadersRead);
            if (response.StatusCode is not (HttpStatusCode.OK or HttpStatusCode.PartialContent))
            {
                throw CreateHttpException(Lang.WebDAVError_DownloadingFile, response.StatusCode);
            }
            await using var input = await response.Content.ReadAsStreamAsync(cancellationToken);
            await using var output = File.Create(localPath);
            var buffer = new byte[64 * 1024];
            long downloadedBytes = 0;
            int read;
            while ((read = await input.ReadAsync(buffer, cancellationToken)) > 0)
            {
                await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
                downloadedBytes += read;
                onProgress?.Invoke(downloadedBytes);
            }
            if (expectedLength is long length && length > 0 && downloadedBytes != length)
            {
                throw new HttpRequestException(Lang.WebDAVError_DownloadSizeMismatch);
            }
        }
        catch
        {
            try
            {
                File.Delete(localPath);
            }
            catch { }
            throw;
        }
    }


    /// <summary>
    /// 检查服务器上的文件是否存在（PROPFIND Depth 0，使用短超时）
    /// </summary>
    public static async Task<bool> CheckFileExistsAsync(string serverAddress, string? userName, string? password, string fileUrl, CancellationToken cancellationToken = default)
    {
        using var httpClient = CreateHttpClient(serverAddress, userName, password, TimeSpan.FromSeconds(10));
        using var request = new HttpRequestMessage(new HttpMethod("PROPFIND"), fileUrl);
        request.Headers.Add("Depth", "0");
        using var response = await SendAsync(httpClient, request, cancellationToken);
        return response.StatusCode switch
        {
            HttpStatusCode.OK or HttpStatusCode.MultiStatus => true,
            HttpStatusCode.NotFound => false,
            _ => throw CreateHttpException(Lang.WebDAVError_CheckingFile, response.StatusCode),
        };
    }


    /// <summary>
    /// 删除服务器上的文件（文件不存在时视为成功）
    /// </summary>
    public static async Task DeleteFileAsync(string serverAddress, string? userName, string? password, string fileUrl, CancellationToken cancellationToken = default)
    {
        using var httpClient = CreateHttpClient(serverAddress, userName, password);
        using var request = new HttpRequestMessage(HttpMethod.Delete, fileUrl);
        using var response = await SendAsync(httpClient, request, cancellationToken);
        if (response.StatusCode is not (HttpStatusCode.OK or HttpStatusCode.NoContent or HttpStatusCode.NotFound))
        {
            throw CreateHttpException(Lang.WebDAVError_DeletingFile, response.StatusCode);
        }
    }


    /// <summary>
    /// 发送请求并统一处理超时与网络层异常，将其包装为带本地化提示的 WebDAV 错误
    /// </summary>
    private static async Task<HttpResponseMessage> SendAsync(HttpClient httpClient, HttpRequestMessage request, CancellationToken cancellationToken, HttpCompletionOption completionOption = HttpCompletionOption.ResponseContentRead)
    {
        try
        {
            return await httpClient.SendAsync(request, completionOption, cancellationToken);
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            // HttpClient.Timeout 触发的超时；用户主动取消则直接透传，不提示
            throw new HttpRequestException(Lang.WebDAVError_Timeout, ex);
        }
        catch (HttpRequestException ex) when (ex.InnerException is not null)
        {
            // 网络层错误：DNS 解析失败、连接被拒、TLS 证书等
            throw new HttpRequestException(Lang.WebDAVError_NetworkError, ex);
        }
    }


    /// <summary>
    /// 根据 HTTP 状态码构造带语义提示的错误消息
    /// </summary>
    private static HttpRequestException CreateHttpException(string action, HttpStatusCode statusCode)
    {
        string hint = statusCode switch
        {
            HttpStatusCode.Unauthorized => Lang.WebDAVError_Unauthorized,
            HttpStatusCode.Forbidden => Lang.WebDAVError_Forbidden,
            HttpStatusCode.MethodNotAllowed => Lang.WebDAVError_MethodNotAllowed,
            HttpStatusCode.RequestTimeout or HttpStatusCode.GatewayTimeout => Lang.WebDAVError_Timeout,
            _ => string.Empty,
        };
        string message = string.Format(Lang.WebDAVError_Failed, action, (int)statusCode);
        return hint.Length > 0 ? new HttpRequestException($"{message} {hint}") : new HttpRequestException(message);
    }


    /// <summary>
    /// 包装上传流以报告上传进度
    /// </summary>
    private sealed class ProgressStream : Stream
    {


        private readonly Stream _inner;

        private readonly long _length;

        private readonly Action<long> _onProgress;


        public ProgressStream(Stream inner, long length, Action<long> onProgress)
        {
            _inner = inner;
            _length = length;
            _onProgress = onProgress;
        }


        public override bool CanRead => _inner.CanRead;

        public override bool CanSeek => _inner.CanSeek;

        public override bool CanWrite => false;

        public override long Length => _length;

        public override long Position { get => _inner.Position; set => _inner.Position = value; }


        public override int Read(byte[] buffer, int offset, int count)
        {
            int read = _inner.Read(buffer, offset, count);
            if (read > 0)
            {
                _onProgress(_inner.Position);
            }
            return read;
        }


        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            int read = await _inner.ReadAsync(buffer, cancellationToken);
            if (read > 0)
            {
                _onProgress(_inner.Position);
            }
            return read;
        }


        public override void Flush() => _inner.Flush();

        public override long Seek(long offset, SeekOrigin origin) => _inner.Seek(offset, origin);

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();


    }


}
