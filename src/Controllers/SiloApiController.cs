using System;
using System.Web.Http;
using System.Net.Http;
using System.Net;
using Newtonsoft.Json;
using IconBusinessOS.Services;
using IconBusinessOS.Models;

namespace IconBusinessOS.Controllers
{
    /// <summary>
    /// ICON BusinessOS Silo API Controller for Sitecore.
    /// 
    /// Endpoints:
    ///   GET /api/icon/v1/heartbeat   → composite silo health (v2.3 contract)
    ///   GET /api/icon/v1/resources   → server resource metrics
    ///   GET /api/icon/v1/security    → security scan summary
    ///   GET /api/icon/v1/content     → content intelligence signals
    ///   GET /api/icon/v1/status      → lightweight liveness check (no auth)
    /// </summary>
    [RoutePrefix("api/icon/v1")]
    public class SiloApiController : ApiController
    {
        private readonly SystemMonitor _systemMonitor;
        private readonly SecurityScanner _securityScanner;
        private readonly ContentIntelligence _contentIntelligence;
        private readonly PhoneHome _phoneHome;

        public SiloApiController()
        {
            _systemMonitor = new SystemMonitor();
            _securityScanner = new SecurityScanner();
            _contentIntelligence = new ContentIntelligence();
            _phoneHome = new PhoneHome(_systemMonitor, _securityScanner, _contentIntelligence);
        }

        /// <summary>
        /// Validate X-Silo-Key header or Sitecore admin session.
        /// </summary>
        private bool IsAuthorized()
        {
            // Check X-Silo-Key header
            if (Request.Headers.Contains("X-Silo-Key"))
            {
                var siloKey = Request.Headers.GetValues("X-Silo-Key").FirstOrDefault();
                var storedKey = Sitecore.Configuration.Settings.GetSetting("IconBusinessOS.SiloApiKey", "");
                if (!string.IsNullOrEmpty(siloKey) && !string.IsNullOrEmpty(storedKey) 
                    && string.Equals(siloKey, storedKey, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            // Check Sitecore admin context
            if (Sitecore.Context.User != null && Sitecore.Context.User.IsAdministrator)
            {
                return true;
            }

            return false;
        }

        private HttpResponseMessage Unauthorized()
        {
            return Request.CreateResponse(HttpStatusCode.Unauthorized, new
            {
                error = "Authentication required. Use X-Silo-Key header or Sitecore admin session."
            });
        }

        /// <summary>
        /// GET /api/icon/v1/heartbeat — Full silo contract v2.3 payload.
        /// </summary>
        [HttpGet]
        [Route("heartbeat")]
        public HttpResponseMessage Heartbeat()
        {
            if (!IsAuthorized()) return Unauthorized();

            var payload = _phoneHome.BuildHeartbeatPayload();
            return Request.CreateResponse(HttpStatusCode.OK, payload);
        }

        /// <summary>
        /// GET /api/icon/v1/resources — Server resource metrics.
        /// </summary>
        [HttpGet]
        [Route("resources")]
        public HttpResponseMessage Resources()
        {
            if (!IsAuthorized()) return Unauthorized();

            var data = _systemMonitor.Collect();
            return Request.CreateResponse(HttpStatusCode.OK, data);
        }

        /// <summary>
        /// GET /api/icon/v1/security — Security scan summary.
        /// </summary>
        [HttpGet]
        [Route("security")]
        public HttpResponseMessage Security()
        {
            if (!IsAuthorized()) return Unauthorized();

            var data = _securityScanner.GetSummary();
            return Request.CreateResponse(HttpStatusCode.OK, data);
        }

        /// <summary>
        /// GET /api/icon/v1/content — Content intelligence signals.
        /// </summary>
        [HttpGet]
        [Route("content")]
        public HttpResponseMessage Content()
        {
            if (!IsAuthorized()) return Unauthorized();

            var data = _contentIntelligence.Collect();
            return Request.CreateResponse(HttpStatusCode.OK, data);
        }

        /// <summary>
        /// GET /api/icon/v1/status — Lightweight liveness check (no auth).
        /// </summary>
        [HttpGet]
        [Route("status")]
        public HttpResponseMessage Status()
        {
            var tenantId = Sitecore.Configuration.Settings.GetSetting("IconBusinessOS.TenantId", "");
            var siloKey = Sitecore.Configuration.Settings.GetSetting("IconBusinessOS.SiloApiKey", "");

            return Request.CreateResponse(HttpStatusCode.OK, new
            {
                status = "ok",
                silo_version = HeartbeatPayload.SILO_VERSION,
                cms = "sitecore",
                cms_version = Sitecore.Configuration.About.GetVersionNumber(false),
                registered = !string.IsNullOrEmpty(siloKey),
                timestamp = DateTime.UtcNow.ToString("o")
            });
        }
    }
}
