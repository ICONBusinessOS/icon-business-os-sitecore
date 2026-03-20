# ICON BusinessOS — Sitecore Module

> Connects your Sitecore CMS (XM/XP/XM Cloud) to the ICON BusinessOS fleet master for infrastructure monitoring, security scanning, and content intelligence. Silo contract v2.3.

## Why Module (Not Plugin)

Sitecore uses **modules** for adding CMS-level functionality (SEO, analytics, integrations). **Plugins** in Sitecore extend developer tools like CLI or Commerce Engine. Since BusinessOS adds health monitoring, REST endpoints, admin UI, and scheduled tasks — a **module** (packaged as `.zip` or `.itempackage`) is the correct approach.

## What This Module Does

### Capabilities
- **System Resources**: disk, memory, CPU, .NET CLR, database, Sitecore cache stats
- **Security Scanning**: config file integrity (SHA-256 baseline), user audit, role permissions
- **Content Intelligence**: item inventory, publishing velocity, workflow queue, media library, language coverage
- **Phone Home**: push composite health to fleet master via Sitecore scheduled task

### API Endpoints (Sitecore API Controller)
| Endpoint | Auth | Description |
|----------|------|-------------|
| `/api/icon/v1/heartbeat` | API Key | Full silo health (v2.3 contract) |
| `/api/icon/v1/resources` | API Key | Server resource metrics |
| `/api/icon/v1/security` | API Key | Security scan summary |
| `/api/icon/v1/content` | API Key | Content intelligence signals |
| `/api/icon/v1/status` | None | Liveness check (fleet discovery) |

## Installation

### Sitecore XM/XP (on-prem or PaaS)
1. Deploy DLL from `bin/` to your Sitecore instance
2. Copy config patch from `App_Config/Include/IconBusinessOS/` to your instance
3. Install serialized items (admin UI, settings template)
4. Configure Tenant ID in `/sitecore/system/Modules/ICON BusinessOS/Settings`

### Sitecore XM Cloud
1. Deploy as a rendering host plugin via Sitecore CLI
2. Register API routes in your Next.js/headless rendering host
3. Configure via environment variables

## Requirements
- Sitecore XM/XP 10.x+ or XM Cloud
- .NET Framework 4.8+ (XM/XP) or .NET 6+ (XM Cloud)
- Sitecore PowerShell Extensions (optional, for enhanced diagnostics)

## Sitecore-Specific Signals

Unlike WordPress/Drupal which are PHP-based, Sitecore runs on .NET/IIS:

| Signal | Source | Notes |
|--------|--------|-------|
| CLR Memory | `GC.GetTotalMemory()` | .NET garbage collection pressure |
| App Pool | IIS management API | Recycling schedule, memory limits |
| Sitecore Cache | `CacheManager.GetStatistics()` | HTML cache, data cache, item cache hit rates |
| Publishing Queue | `PublishManager.GetPublishStatus()` | Queue depth, last publish time |
| Workflow | Workbox API | Items pending approval, avg approval time |
| xDB (XP only) | xConnect API | Contact count, interaction trends |
| Language Coverage | Item API | Content translated vs untranslated per language |

## File Structure

```
icon-business-os-sitecore/
├── README.md
├── src/
│   ├── Controllers/
│   │   └── SiloApiController.cs       # API endpoints
│   ├── Services/
│   │   ├── SystemMonitor.cs            # .NET/IIS/Sitecore resource metrics
│   │   ├── SecurityScanner.cs          # Config integrity, user audit
│   │   ├── ContentIntelligence.cs      # Item inventory, workflow, media
│   │   └── PhoneHome.cs               # Fleet master communication
│   ├── Pipelines/
│   │   └── InitializeRoutes.cs         # Register API routes
│   ├── Commands/
│   │   └── PhoneHomeCommand.cs         # Sitecore scheduled task
│   └── Models/
│       └── HeartbeatPayload.cs         # Silo contract v2.3 model
├── serialization/
│   └── App_Config/Include/IconBusinessOS/
│       └── IconBusinessOS.config       # Sitecore config patch
└── .gitignore
```

## Silo Contract v2.3
Same heartbeat payload schema as WordPress/Drupal/Omeka plugins — CMS-agnostic.

## License
MIT (compatible with Sitecore Marketplace requirements)
