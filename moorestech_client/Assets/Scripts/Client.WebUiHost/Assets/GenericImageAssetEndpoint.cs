using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using UnityEngine;

namespace Client.WebUiHost.Assets
{
    public static class GenericImageAssetEndpoint
    {
        public const string PathPrefix = "/api/assets/";
        private static readonly Regex SafeSegment = new("^[a-z0-9][a-z0-9._-]*$", RegexOptions.IgnoreCase);
        private static readonly Regex HashedName = new("[._-][a-f0-9]{8,64}$", RegexOptions.IgnoreCase);
        private static readonly Dictionary<string, string> MimeTypes = new(StringComparer.OrdinalIgnoreCase)
        {
            [".png"] = "image/png", [".jpg"] = "image/jpeg", [".jpeg"] = "image/jpeg",
            [".webp"] = "image/webp", [".gif"] = "image/gif", [".svg"] = "image/svg+xml",
        };

        public static async Task HandleAsync(HttpContext context, string path)
        {
            var relative = path.Substring(PathPrefix.Length);
            var segments = relative.Split('/');
            if (segments.Length != 2 || !SafeSegment.IsMatch(segments[0]) || !SafeSegment.IsMatch(segments[1]))
            {
                context.Response.StatusCode = 404;
                return;
            }

            var extension = Path.GetExtension(segments[1]);
            if (!MimeTypes.TryGetValue(extension, out var mime))
            {
                context.Response.StatusCode = 415;
                return;
            }

            // 安全な2セグメントに制限する
            // Restrict paths to two safe segments
            var filePath = Path.Combine(Application.streamingAssetsPath, "WebUiAssets", segments[0], segments[1]);
            if (!File.Exists(filePath))
            {
                context.Response.StatusCode = 404;
                return;
            }

            var etag = ComputeETag(filePath);
            context.Response.Headers["ETag"] = etag;
            context.Response.Headers["X-Content-Type-Options"] = "nosniff";
            context.Response.Headers["Cache-Control"] = IsContentAddressed(segments[1])
                ? "public, max-age=31536000, immutable"
                : "no-cache";
            if (context.Request.Headers["If-None-Match"].ToString() == etag)
            {
                context.Response.StatusCode = 304;
                return;
            }

            context.Response.ContentType = mime;
            await context.Response.SendFileAsync(filePath);
        }

        private static bool IsContentAddressed(string fileName)
        {
            return HashedName.IsMatch(Path.GetFileNameWithoutExtension(fileName));
        }

        private static string ComputeETag(string filePath)
        {
            using var stream = File.OpenRead(filePath);
            using var sha = SHA256.Create();
            return "\"" + Convert.ToBase64String(sha.ComputeHash(stream)) + "\"";
        }
    }
}
