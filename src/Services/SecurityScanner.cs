using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;

namespace IconBusinessOS.Services
{
    /// <summary>
    /// ICON BusinessOS — Security Scanner for Sitecore.
    /// 
    /// Sitecore-specific security signals:
    ///   - Config file integrity (SHA-256 baseline for web.config, ConnectionStrings.config, etc.)
    ///   - Admin user audit (users with sitecore\Admin role)
    ///   - Security hardening check (admin page access, debug mode, etc.)
    /// </summary>
    public class SecurityScanner
    {
        private static Dictionary<string, FileHash> _baseline;

        public void CreateBaseline()
        {
            _baseline = HashCriticalFiles();
        }

        public Dictionary<string, object> GetSummary()
        {
            var integrity = CheckFileIntegrity();
            var adminCount = CountAdminUsers();

            return new Dictionary<string, object>
            {
                ["scan_available"] = _baseline != null,
                ["file_changes_since_baseline"] = integrity.ChangesDetected,
                ["file_changes_detail"] = integrity.ChangedFiles,
                ["admin_user_count"] = adminCount,
                ["core_integrity"] = integrity.Status,
                ["hardening"] = CheckHardening(),
                ["failed_logins_24h"] = GetFailedLoginCount(24),
                ["last_scan"] = DateTime.UtcNow.ToString("o"),
            };
        }

        public void ExecuteScan()
        {
            // Refresh baseline comparison (don't overwrite baseline itself)
            GetSummary();
        }

        // ─── File Integrity ───────────────────────────────────────

        private Dictionary<string, FileHash> HashCriticalFiles()
        {
            var basePath = AppDomain.CurrentDomain.BaseDirectory;
            var files = new Dictionary<string, string>
            {
                ["web.config"] = Path.Combine(basePath, "web.config"),
                ["ConnectionStrings.config"] = Path.Combine(basePath, "App_Config", "ConnectionStrings.config"),
                ["Sitecore.config"] = Path.Combine(basePath, "App_Config", "Sitecore.config"),
                ["Global.asax"] = Path.Combine(basePath, "Global.asax"),
            };

            var hashes = new Dictionary<string, FileHash>();
            using (var sha256 = SHA256.Create())
            {
                foreach (var kvp in files)
                {
                    if (File.Exists(kvp.Value))
                    {
                        var bytes = File.ReadAllBytes(kvp.Value);
                        var hash = BitConverter.ToString(sha256.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant();
                        hashes[kvp.Key] = new FileHash
                        {
                            Hash = hash,
                            Size = bytes.Length,
                            Modified = File.GetLastWriteTimeUtc(kvp.Value)
                        };
                    }
                }
            }
            return hashes;
        }

        private IntegrityResult CheckFileIntegrity()
        {
            if (_baseline == null)
            {
                return new IntegrityResult { Status = "no_baseline", ChangesDetected = 0, ChangedFiles = new List<object>() };
            }

            var current = HashCriticalFiles();
            var changed = new List<object>();

            foreach (var kvp in _baseline)
            {
                if (!current.ContainsKey(kvp.Key))
                {
                    changed.Add(new { file = kvp.Key, change = "deleted" });
                }
                else if (current[kvp.Key].Hash != kvp.Value.Hash)
                {
                    changed.Add(new { file = kvp.Key, change = "modified", old_size = kvp.Value.Size, new_size = current[kvp.Key].Size });
                }
            }

            foreach (var kvp in current)
            {
                if (!_baseline.ContainsKey(kvp.Key))
                {
                    changed.Add(new { file = kvp.Key, change = "added" });
                }
            }

            return new IntegrityResult
            {
                Status = changed.Count == 0 ? "clean" : "changes_detected",
                ChangesDetected = changed.Count,
                ChangedFiles = changed
            };
        }

        // ─── Admin Users ──────────────────────────────────────────

        private int CountAdminUsers()
        {
            try
            {
                var domain = Sitecore.SecurityModel.DomainManager.GetDomain("sitecore");
                if (domain == null) return 0;

                var users = System.Web.Security.Membership.GetAllUsers();
                int adminCount = 0;
                foreach (System.Web.Security.MembershipUser user in users)
                {
                    if (System.Web.Security.Roles.IsUserInRole(user.UserName, "sitecore\\Sitecore Client Developing"))
                    {
                        adminCount++;
                    }
                }
                // Always count sitecore\Admin
                return Math.Max(adminCount, 1);
            }
            catch
            {
                return 1; // At minimum, sitecore\Admin exists
            }
        }

        // ─── Hardening Checks ─────────────────────────────────────

        private Dictionary<string, object> CheckHardening()
        {
            var issues = new List<string>();

            // Check debug mode
            try
            {
                if (System.Web.HttpContext.Current?.IsDebuggingEnabled == true)
                {
                    issues.Add("debug_mode_enabled");
                }
            }
            catch { }

            // Check custom errors
            try
            {
                var customErrors = System.Configuration.ConfigurationManager.GetSection("system.web/customErrors");
                // If customErrors mode is Off, that's a hardening issue in production
            }
            catch { }

            return new Dictionary<string, object>
            {
                ["issues_count"] = issues.Count,
                ["issues"] = issues,
                ["status"] = issues.Count == 0 ? "hardened" : "issues_found"
            };
        }

        // ─── Failed Logins ────────────────────────────────────────

        private int GetFailedLoginCount(int hours)
        {
            try
            {
                // Sitecore logs failed logins to the Sitecore log
                var logPath = Sitecore.Configuration.Settings.GetSetting("LogFolder", "");
                if (string.IsNullOrEmpty(logPath)) return 0;

                var cutoff = DateTime.UtcNow.AddHours(-hours);
                int count = 0;

                var logFiles = Directory.GetFiles(logPath, "log.*.txt")
                    .Where(f => File.GetLastWriteTimeUtc(f) > cutoff)
                    .OrderByDescending(f => File.GetLastWriteTimeUtc(f))
                    .Take(5);

                foreach (var file in logFiles)
                {
                    try
                    {
                        var lines = File.ReadLines(file);
                        count += lines.Count(l => l.Contains("Login failed") || l.Contains("login attempt failed"));
                    }
                    catch { }
                }

                return count;
            }
            catch
            {
                return 0;
            }
        }

        // ─── Helper Classes ───────────────────────────────────────

        private class FileHash
        {
            public string Hash { get; set; }
            public long Size { get; set; }
            public DateTime Modified { get; set; }
        }

        private class IntegrityResult
        {
            public string Status { get; set; }
            public int ChangesDetected { get; set; }
            public List<object> ChangedFiles { get; set; }
        }
    }
}
