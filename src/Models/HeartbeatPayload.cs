using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace IconBusinessOS.Models
{
    /// <summary>
    /// Silo contract v2.3 heartbeat payload model.
    /// CMS-agnostic schema shared across WordPress, Drupal, Omeka, Sitecore.
    /// </summary>
    public class HeartbeatPayload
    {
        public const string SILO_VERSION = "2.3.0";
        public const string PLUGIN_VERSION = "1.0.0";

        [JsonProperty("silo_version")]
        public string SiloVersion { get; set; } = SILO_VERSION;

        [JsonProperty("plugin_version")]
        public string PluginVersion { get; set; } = PLUGIN_VERSION;

        [JsonProperty("tenant_id")]
        public string TenantId { get; set; }

        [JsonProperty("site_url")]
        public string SiteUrl { get; set; }

        [JsonProperty("site_name")]
        public string SiteName { get; set; }

        [JsonProperty("cms")]
        public string Cms { get; set; } = "sitecore";

        [JsonProperty("cms_version")]
        public string CmsVersion { get; set; }

        [JsonProperty("runtime_version")]
        public string RuntimeVersion { get; set; }

        [JsonProperty("health_score")]
        public int HealthScore { get; set; }

        [JsonProperty("health_grade")]
        public string HealthGrade { get; set; }

        [JsonProperty("system_resources")]
        public Dictionary<string, object> SystemResources { get; set; }

        [JsonProperty("security")]
        public Dictionary<string, object> Security { get; set; }

        [JsonProperty("content")]
        public Dictionary<string, object> Content { get; set; }

        [JsonProperty("capabilities")]
        public List<string> Capabilities { get; set; } = new List<string>
        {
            "http_probes", "system_resources", "security_scanning",
            "content_intelligence", "phone_home", "silo_heartbeat"
        };

        [JsonProperty("timestamp")]
        public string Timestamp { get; set; } = DateTime.UtcNow.ToString("o");

        /// <summary>
        /// Convert score to letter grade.
        /// </summary>
        public static string ScoreToGrade(int score)
        {
            if (score >= 97) return "A+";
            if (score >= 93) return "A";
            if (score >= 90) return "A-";
            if (score >= 87) return "B+";
            if (score >= 83) return "B";
            if (score >= 80) return "B-";
            if (score >= 77) return "C+";
            if (score >= 73) return "C";
            if (score >= 70) return "C-";
            if (score >= 60) return "D";
            return "F";
        }
    }
}
