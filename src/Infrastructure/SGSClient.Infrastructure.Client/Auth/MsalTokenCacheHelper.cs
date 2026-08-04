using Microsoft.Identity.Client;
using System.Runtime.InteropServices;
using System.Security.Cryptography;

namespace SGSClient.Infrastructure.Client.Auth;

public static class MsalTokenCacheHelper
{
    private static readonly string CacheFilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "SGSClient",
        "msal_cache.bin3");

    private static readonly object FileLock = new();

    public static void EnableSerialization(ITokenCache tokenCache)
    {
        tokenCache.SetBeforeAccess(BeforeAccessNotification);
        tokenCache.SetAfterAccess(AfterAccessNotification);
    }

    private static void BeforeAccessNotification(TokenCacheNotificationArgs args)
    {
        lock (FileLock)
        {
            if (!File.Exists(CacheFilePath))
            {
                return;
            }

            var data = File.ReadAllBytes(CacheFilePath);
            args.TokenCache.DeserializeMsalV3(Unprotect(data));
        }
    }

    private static void AfterAccessNotification(TokenCacheNotificationArgs args)
    {
        if (!args.HasStateChanged)
        {
            return;
        }

        lock (FileLock)
        {
            var directory = Path.GetDirectoryName(CacheFilePath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var data = args.TokenCache.SerializeMsalV3();
            File.WriteAllBytes(CacheFilePath, Protect(data));
        }
    }

    private static byte[] Protect(byte[] data)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return ProtectedData.Protect(data, null, DataProtectionScope.CurrentUser);
        }

        return data;
    }

    private static byte[] Unprotect(byte[] data)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return ProtectedData.Unprotect(data, null, DataProtectionScope.CurrentUser);
        }

        return data;
    }
}
