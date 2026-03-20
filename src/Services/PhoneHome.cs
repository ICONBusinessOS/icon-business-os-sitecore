using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using IconBusinessOS.Models;

namespace IconBusinessOS.Services
{
    /// <summary>
    /// ICON BusinessOS — Phone Home service for Sitecore.
    /// 
    /// Pushes composite health payload to fleet master.
    /// Called by the Sitecore scheduled task (PhoneHomeCommand).
    /// </summary>
    public class PhoneHome
    {
        private static readonly HttpClient _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };

        private readonly SystemMonitor _systemMonitor;
        private readonly SecurityScanner _securityScanner;
        private readonly ContentIntelligence _contentIntelligence;

        public PhoneHome(SystemMonitor systemMonitor, SecurityScanner securityScanner, ContentIntelligence contentIntelligence)
        {
            _systemMonitor = systemMonitor;
            _securityScanner = securityScanner;
            _contentIntelligence = contentIntelligence;
        }

        /// <summary>
        /// Build the full silo contract v2.3 heartbeat payload.
        /// </summary>
        public HeartbeatPayload BuildHeartbeatPayload()
        {
            var system = _systemMonitor.Collect();
            var security = _securityScanner.GetSummary();
            var content = _contentIntelligence.Collect();

            var score = ComputeHealthScore(system, security);

            var tenantId = Sitecore.Configuration.Settings.GetSetting("IconBusinessOS.TenantId", "");
            var siteUrl = Sitecore.Web.WebUtil.GetServerUrl();

            return new HeartbeatPayload
            {
                TenantId = tenantId,
                SiteUrl = siteUrl,
                SiteName = Sitecore.Configuration.Settings.GetSetting("IconBusinessOS.SiteName", "Sitecore Site"),
                CmsVersion = Sitecore.Configuration.About.GetVersionNumber(false),
                RuntimeVersion = Environment.Version.ToString(),
                HealthScore = score,
                HealthGrade = HeartbeatPayload.ScoreToGrade(score),
                SystemResources = new Dictionary<string, object>
                {
                    ["disk_usage_pct"] = system.ContainsKey("disk_usage_pct") ? system["disk_usage_pct"] : null,
                    ["clr_memory_mb"] = system.ContainsKey("clr_memory_mb") ? system["clr_memory_mb"] : null,
                    ["process_memory_mb"] = system.ContainsKey("process_memory_mb") ? system["process_memory_mb"] : null,
                    ["process_threads"] = system.ContainsKey("process_threads") ? system["process_threads"] : null,
                    ["sitecore_cache_usage_pct"] = system.ContainsKey("sitecore_cache_usage_pct") ? system["sitecore_cache_usage_pct"] : null,
                    ["db_size_mb"] = system.ContainsKey("db_size_mb") ? system["db_size_mb"] : null,
                },
                Security = security,
                Content = content,
            };
        }

        /// <summary>
        /// Register this silo with the fleet master.
        /// </summary>
        public async Task<Dictionary<string, object>> RegisterAsync()
        {
            var tenantId = Sitecore.Configuration.Settings.GetSetting("IconBusinessOS.TenantId", "");
            if (string.IsNullOrEmpty(tenantId))
            {
                return new Dictionary<string, object> { ["success"] = false, ["error"] = "Tenant ID is required." };
            }

            var fleetUrl = Sitecore.Configuration.Settings.GetSetting("IconBusinessOS.FleetMasterUrl", "https://os.theicon.ai/api/silo");
            var siteUrl = Sitecore.Web.WebUtil.GetServerUrl();

            var payload = new
            {
                tenant_id = tenantId,
                site_url = siteUrl,
                site_name = Sitecore.Configuration.Settings.GetSetting("IconBusinessOS.SiteName", "Sitecore Site"),
                cms = "sitecore",
                cms_version = Sitecore.Configuration.About.GetVersionNumber(false),
                runtime_version = Environment.Version.ToString(),
                plugin_version = HeartbeatPayload.PLUGIN_VERSION,
                silo_version = HeartbeatPayload.SILO_VERSION,
                heartbeat_url = siteUrl + "/api/icon/v1/heartbeat",
                status_url = siteUrl + "/api/icon/v1/status",
                capabilities = new[] { "http_probes", "system_resources", "security_scanning", "content_intelligence", "phone_home", "silo_heartbeat" },
                registered_at = DateTime.UtcNow.ToString("o"),
            };

            try
            {
                var json = JsonConvert.SerializeObject(payload);
                var httpContent = new StringContent(json, Encoding.UTF8, "application/json");
                var response = await _httpClient.PostAsync(fleetUrl + "/register", httpContent);
                var body = await response.Content.ReadAsStringAsync();
                var result = JsonConvert.DeserializeObject<Dictionary<string, object>>(body);

                if (result != null && result.ContainsKey("silo_api_key"))
                {
                    // Store API key in Sitecore settings (would need a config patch or custom storage)
                    Sitecore.Configuration.Settings.SetSetting("IconBusinessOS.SiloApiKey", result["silo_api_key"].ToString());
                    return new Dictionary<string, object> { ["success"] = true, ["silo_api_key"] = result["silo_api_key"] };
                }

                return new Dictionary<string, object> { ["success"] = false, ["error"] = result?["error"]?.ToString() ?? "No API key returned." };
            }
            catch (Exception ex)
            {
                return new Dictionary<string, object> { ["success"] = false, ["error"] = ex.Message };
            }
        }

        /// <summary>
        /// Execute phone-home heartbeat push (called by scheduled task).
        /// </summary>
        public async Task<Dictionary<string, object>> ExecuteAsync()
        {
            var siloKey = Sitecore.Configuration.Settings.GetSetting("IconBusinessOS.SiloApiKey", "");
            if (string.IsNullOrEmpty(siloKey))
            {
                return new Dictionary<string, object> { ["success"] = false, ["error"] = "Not registered with fleet master." };
            }

            var fleetUrl = Sitecore.Configuration.Settings.GetSetting("IconBusinessOS.FleetMasterUrl", "https://os.theicon.ai/api/silo");
            var tenantId = Sitecore.Configuration.Settings.GetSetting("IconBusinessOS.TenantId", "");

            var payload = BuildHeartbeatPayload();
            var json = JsonConvert.SerializeObject(payload);

            try
            {
                var request = new HttpRequestMessage(HttpMethod.Post, fleetUrl + "/heartbeat")
                {
                    Content = new StringContent(json, Encoding.UTF8, "application/json")
                };
                request.Headers.Add("X-Silo-Key", siloKey);
                request.Headers.Add("X-Tenant-ID", tenantId);
                request.Headers.Add("Accept", "application/json");

                var response = await _httpClient.SendAsync(request);
                var body = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    // Process fleet commands if any
                    var result = JsonConvert.DeserializeObject<Dictionary<string, object>>(body);
                    if (result != null && result.ContainsKey("commands"))
                    {
                        ProcessFleetCommands(result["commands"]);
                    }
                    return new Dictionary<string, object> { ["success"] = true, ["score"] = payload.HealthScore };
                }

                return new Dictionary<string, object> { ["success"] = false, ["error"] = $"HTTP {(int)response.StatusCode}: {body}" };
            }
            catch (Exception ex)
            {
                return new Dictionary<string, object> { ["success"] = false, ["error"] = ex.Message };
            }
        }

        private void ProcessFleetCommands(object commandsObj)
        {
            try
            {
                var commands = JsonConvert.DeserializeObject<List<Dictionary<string, string>>>(commandsObj.ToString());
                if (commands == null) return;

                foreach (var cmd in commands)
                {
                    var action = cmd.ContainsKey("action") ? cmd["action"] : "";
                    switch (action)
                    {
                        case "refresh_baseline":
                            _securityScanner.CreateBaseline();
                            break;
                        case "run_security_scan":
                            _securityScanner.ExecuteScan();
                            break;
                    }
                }
            }
            catch { }
        }

        /// <summary>
        /// Compute composite health score (0-100).
        /// </summary>
        private int ComputeHealthScore(Dictionary<string, object> system, Dictionary<string, object> security)
        {
            int score = 100;

            // Disk
            if (system.ContainsKey("disk_usage_pct") && system["disk_usage_pct"] is double disk)
            {
                if (disk > 90) score -= 25;
                else if (disk > 80) score -= 15;
                else if (disk > 70) score -= 5;
            }

            // CLR Memory pressure (>1GB working set is concerning)
            if (system.ContainsKey("process_memory_mb") && system["process_memory_mb"] is double mem)
            {
                if (mem > 4096) score -= 20;
                else if (mem > 2048) score -= 10;
                else if (mem > 1024) score -= 3;
            }

            // Cache pressure
            if (system.ContainsKey("sitecore_cache_usage_pct") && system["sitecore_cache_usage_pct"] is double cache)
            {
                if (cache > 95) score -= 15;
                else if (cache > 85) score -= 5;
            }

            // File changes
            if (security.ContainsKey("file_changes_since_baseline") && security["file_changes_since_baseline"] is int changes)
            {
                if (changes > 0) score -= Math.Min(changes * 5, 20);
            }

            // Failed logins
            if (security.ContainsKey("failed_logins_24h") && security["failed_logins_24h"] is int logins)
            {
                if (logins > 50) score -= 15;
                else if (logins > 10) score -= 5;
            }

            return Math.Max(0, Math.Min(100, score));
        }
    }
}
