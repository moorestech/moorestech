using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace Client.WebUiHost.Static
{
    /// <summary>
    /// 検証済みdistを配信規約に従って返す
    /// Serves a validated dist with explicit MIME, caching, and SPA fallback rules
    /// </summary>
    public class WebUiStaticFileEndpoint
    {
        private readonly string _rootPath;
        private static readonly Dictionary<string, string> MimeTypes = new(StringComparer.OrdinalIgnoreCase)
        {
            [".html"] = "text/html; charset=utf-8", [".js"] = "text/javascript; charset=utf-8",
            [".css"] = "text/css; charset=utf-8", [".json"] = "application/json; charset=utf-8",
            [".svg"] = "image/svg+xml", [".png"] = "image/png", [".jpg"] = "image/jpeg",
            [".jpeg"] = "image/jpeg", [".webp"] = "image/webp", [".gif"] = "image/gif",
            [".ico"] = "image/x-icon", [".woff"] = "font/woff", [".woff2"] = "font/woff2",
            [".ttf"] = "font/ttf", [".otf"] = "font/otf", [".wasm"] = "application/wasm", [".map"] = "application/json",
        };

        public WebUiStaticFileEndpoint(string rootPath)
        {
            _rootPath = rootPath;
        }

        public async Task HandleAsync(HttpContext context, string requestPath)
        {
            var relativePath = requestPath.TrimStart('/');
            if (string.IsNullOrEmpty(relativePath)) relativePath = "index.html";

            // 画面ルートだけindexへ戻す
            // Fall back only extensionless client routes to index; missing assets remain 404
            if (!WebUiArtifactValidator.TryResolveFile(_rootPath, relativePath, out var filePath))
            {
                context.Response.StatusCode = 404;
                return;
            }
            if (!File.Exists(filePath) && string.IsNullOrEmpty(Path.GetExtension(relativePath)))
                filePath = Path.Combine(_rootPath, "index.html");
            if (!File.Exists(filePath))
            {
                context.Response.StatusCode = 404;
                return;
            }

            var extension = Path.GetExtension(filePath);
            if (!MimeTypes.TryGetValue(extension, out var mime))
            {
                context.Response.StatusCode = 415;
                return;
            }

            context.Response.ContentType = mime;
            context.Response.Headers["X-Content-Type-Options"] = "nosniff";
            context.Response.Headers["Cache-Control"] = relativePath.StartsWith("assets/", StringComparison.Ordinal)
                ? "public, max-age=31536000, immutable"
                : "no-cache";
            await context.Response.SendFileAsync(filePath);
        }
    }
}
