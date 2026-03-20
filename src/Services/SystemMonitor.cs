using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime;

namespace IconBusinessOS.Services
{
    /// <summary>
    /// ICON BusinessOS — System Monitor for Sitecore.
    /// 
    /// Collects server-level resource metrics from within the .NET/IIS environment.
    /// Sitecore-specific: CLR memory, Sitecore cache stats, app pool info.
    /// </summary>
    public class SystemMonitor
    {
        public Dictionary<string, object> Collect()
        {
            var data = new Dictionary<string, object>();

            // Disk
            try
            {
                var drive = new DriveInfo(Path.GetPathRoot(AppDomain.CurrentDomain.BaseDirectory));
                data["disk_total_gb"] = Math.Round(drive.TotalSize / 1073741824.0, 1);
                data["disk_free_gb"] = Math.Round(drive.AvailableFreeSpace / 1073741824.0, 1);
                data["disk_used_gb"] = Math.Round((drive.TotalSize - drive.AvailableFreeSpace) / 1073741824.0, 1);
                data["disk_usage_pct"] = Math.Round(((drive.TotalSize - drive.AvailableFreeSpace) / (double)drive.TotalSize) * 100, 1);
            }
            catch
            {
                data["disk_usage_pct"] = null;
            }

            // CLR Memory (.NET specific)
            data["clr_memory_mb"] = Math.Round(GC.GetTotalMemory(false) / 1048576.0, 1);
            data["clr_memory_peak_mb"] = Math.Round(Process.GetCurrentProcess().PeakWorkingSet64 / 1048576.0, 1);
            data["clr_gc_gen0_collections"] = GC.CollectionCount(0);
            data["clr_gc_gen1_collections"] = GC.CollectionCount(1);
            data["clr_gc_gen2_collections"] = GC.CollectionCount(2);
            data["clr_is_server_gc"] = GCSettings.IsServerGC;

            // Process memory
            var process = Process.GetCurrentProcess();
            data["process_memory_mb"] = Math.Round(process.WorkingSet64 / 1048576.0, 1);
            data["process_private_mb"] = Math.Round(process.PrivateMemorySize64 / 1048576.0, 1);
            data["process_threads"] = process.Threads.Count;
            data["process_handles"] = process.HandleCount;
            data["process_uptime_hours"] = Math.Round((DateTime.Now - process.StartTime).TotalHours, 1);

            // CPU (process level)
            data["process_cpu_time_seconds"] = Math.Round(process.TotalProcessorTime.TotalSeconds, 1);

            // Sitecore version
            try
            {
                data["sitecore_version"] = Sitecore.Configuration.About.GetVersionNumber(false);
                data["sitecore_revision"] = Sitecore.Configuration.About.GetVersionNumber(true);
            }
            catch
            {
                data["sitecore_version"] = "unknown";
            }

            // Runtime info
            data["runtime_version"] = Environment.Version.ToString();
            data["os_version"] = Environment.OSVersion.ToString();
            data["processor_count"] = Environment.ProcessorCount;
            data["is_64bit"] = Environment.Is64BitProcess;

            // Sitecore Cache Stats
            try
            {
                var caches = Sitecore.Caching.CacheManager.GetAllCaches();
                long totalCacheSize = 0;
                long totalCacheMaxSize = 0;
                int cacheCount = 0;

                foreach (var cache in caches)
                {
                    totalCacheSize += cache.Size;
                    totalCacheMaxSize += cache.MaxSize;
                    cacheCount++;
                }

                data["sitecore_cache_count"] = cacheCount;
                data["sitecore_cache_used_mb"] = Math.Round(totalCacheSize / 1048576.0, 1);
                data["sitecore_cache_max_mb"] = Math.Round(totalCacheMaxSize / 1048576.0, 1);
                data["sitecore_cache_usage_pct"] = totalCacheMaxSize > 0
                    ? Math.Round((totalCacheSize / (double)totalCacheMaxSize) * 100, 1)
                    : 0;
            }
            catch
            {
                data["sitecore_cache_count"] = 0;
            }

            // Database size
            data["db_size_mb"] = GetDatabaseSize();

            // Sitecore item count (master DB)
            try
            {
                var masterDb = Sitecore.Configuration.Factory.GetDatabase("master");
                data["sitecore_item_count"] = masterDb != null
                    ? masterDb.GetItem("/sitecore")?.Axes.GetDescendants().Length ?? 0
                    : 0;
            }
            catch
            {
                data["sitecore_item_count"] = null;
            }

            data["collected_at"] = DateTime.UtcNow.ToString("o");

            return data;
        }

        public Dictionary<string, object> GetModuleInventory()
        {
            // Sitecore doesn't have a module list API like WordPress/Drupal
            // Instead, enumerate installed assemblies in the bin folder
            var binPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "bin");
            var assemblies = new List<Dictionary<string, object>>();

            if (Directory.Exists(binPath))
            {
                var dlls = Directory.GetFiles(binPath, "*.dll");
                foreach (var dll in dlls.Where(d => d.Contains("Sitecore") || d.Contains("Icon")))
                {
                    try
                    {
                        var info = FileVersionInfo.GetVersionInfo(dll);
                        assemblies.Add(new Dictionary<string, object>
                        {
                            ["name"] = Path.GetFileNameWithoutExtension(dll),
                            ["version"] = info.FileVersion ?? "unknown",
                            ["product"] = info.ProductName ?? "",
                            ["size_kb"] = Math.Round(new FileInfo(dll).Length / 1024.0, 1)
                        });
                    }
                    catch { }
                }
            }

            return new Dictionary<string, object>
            {
                ["total_assemblies"] = assemblies.Count,
                ["assemblies"] = assemblies,
                ["collected_at"] = DateTime.UtcNow.ToString("o")
            };
        }

        private object GetDatabaseSize()
        {
            try
            {
                var connectionString = Sitecore.Configuration.Settings.GetConnectionString("master");
                if (string.IsNullOrEmpty(connectionString)) return null;

                using (var conn = new System.Data.SqlClient.SqlConnection(connectionString))
                {
                    conn.Open();
                    using (var cmd = conn.CreateCommand())
                    {
                        cmd.CommandText = "SELECT ROUND(SUM(size * 8.0 / 1024), 1) FROM sys.database_files";
                        var result = cmd.ExecuteScalar();
                        return result != DBNull.Value ? Convert.ToDouble(result) : (object)null;
                    }
                }
            }
            catch
            {
                return null;
            }
        }
    }
}
