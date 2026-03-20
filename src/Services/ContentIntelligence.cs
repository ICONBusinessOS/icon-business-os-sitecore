using System;
using System.Collections.Generic;
using System.Linq;

namespace IconBusinessOS.Services
{
    /// <summary>
    /// ICON BusinessOS — Content Intelligence for Sitecore.
    /// 
    /// Sitecore-specific content signals:
    ///   - Item inventory (master DB item counts by template)
    ///   - Publishing velocity (items published in 7d/30d)
    ///   - Workflow queue depth (items pending approval)
    ///   - Media library health (file count, storage, types)
    ///   - Language coverage (translated vs untranslated per language)
    /// </summary>
    public class ContentIntelligence
    {
        public Dictionary<string, object> Collect()
        {
            return new Dictionary<string, object>
            {
                ["publishing"] = GetPublishingVelocity(),
                ["inventory"] = GetItemInventory(),
                ["workflow"] = GetWorkflowStatus(),
                ["media"] = GetMediaLibraryHealth(),
                ["languages"] = GetLanguageCoverage(),
                ["collected_at"] = DateTime.UtcNow.ToString("o")
            };
        }

        // ─── Publishing Velocity ──────────────────────────────────

        private Dictionary<string, object> GetPublishingVelocity()
        {
            try
            {
                var masterDb = Sitecore.Configuration.Factory.GetDatabase("master");
                var webDb = Sitecore.Configuration.Factory.GetDatabase("web");
                if (masterDb == null) return new Dictionary<string, object> { ["available"] = false };

                var now = DateTime.UtcNow;
                var sevenDaysAgo = now.AddDays(-7);
                var thirtyDaysAgo = now.AddDays(-30);

                // Count recently updated items in master
                var contentRoot = masterDb.GetItem("/sitecore/content");
                if (contentRoot == null) return new Dictionary<string, object> { ["available"] = false };

                var allContent = contentRoot.Axes.GetDescendants();
                int updated7d = 0, updated30d = 0, totalItems = 0;

                foreach (var item in allContent)
                {
                    totalItems++;
                    var updated = item.Statistics.Updated;
                    if (updated > sevenDaysAgo) updated7d++;
                    if (updated > thirtyDaysAgo) updated30d++;
                }

                // Publishing queue
                var publishingTargets = Sitecore.Publishing.PublishManager.GetPublishingTargets(masterDb);

                return new Dictionary<string, object>
                {
                    ["available"] = true,
                    ["total_content_items"] = totalItems,
                    ["updated_7d"] = updated7d,
                    ["updated_30d"] = updated30d,
                    ["velocity_trend"] = updated7d >= (updated30d / 4) ? "on_pace" : "slowing",
                    ["publishing_targets"] = publishingTargets.Length
                };
            }
            catch (Exception ex)
            {
                return new Dictionary<string, object> { ["available"] = false, ["error"] = ex.Message };
            }
        }

        // ─── Item Inventory ───────────────────────────────────────

        private Dictionary<string, object> GetItemInventory()
        {
            try
            {
                var masterDb = Sitecore.Configuration.Factory.GetDatabase("master");
                if (masterDb == null) return new Dictionary<string, object> { ["available"] = false };

                var contentRoot = masterDb.GetItem("/sitecore/content");
                var mediaRoot = masterDb.GetItem("/sitecore/media library");
                var templateRoot = masterDb.GetItem("/sitecore/templates");
                var layoutRoot = masterDb.GetItem("/sitecore/layout");

                return new Dictionary<string, object>
                {
                    ["content_items"] = contentRoot?.Axes.GetDescendants().Length ?? 0,
                    ["media_items"] = mediaRoot?.Axes.GetDescendants().Length ?? 0,
                    ["templates"] = templateRoot?.Axes.GetDescendants().Length ?? 0,
                    ["layouts_renderings"] = layoutRoot?.Axes.GetDescendants().Length ?? 0,
                };
            }
            catch (Exception ex)
            {
                return new Dictionary<string, object> { ["available"] = false, ["error"] = ex.Message };
            }
        }

        // ─── Workflow ─────────────────────────────────────────────

        private Dictionary<string, object> GetWorkflowStatus()
        {
            try
            {
                var masterDb = Sitecore.Configuration.Factory.GetDatabase("master");
                if (masterDb == null) return new Dictionary<string, object> { ["available"] = false };

                var workflowProvider = masterDb.WorkflowProvider;
                if (workflowProvider == null) return new Dictionary<string, object> { ["available"] = false };

                var workflows = workflowProvider.GetWorkflows();
                int totalPending = 0;
                var workflowDetails = new List<Dictionary<string, object>>();

                foreach (var wf in workflows)
                {
                    var states = wf.GetStates();
                    foreach (var state in states)
                    {
                        var items = wf.GetItems(state.StateID);
                        if (items != null && items.Length > 0)
                        {
                            totalPending += items.Length;
                            workflowDetails.Add(new Dictionary<string, object>
                            {
                                ["workflow"] = wf.Appearance.DisplayName,
                                ["state"] = state.DisplayName,
                                ["item_count"] = items.Length
                            });
                        }
                    }
                }

                return new Dictionary<string, object>
                {
                    ["available"] = true,
                    ["total_pending"] = totalPending,
                    ["workflow_states"] = workflowDetails
                };
            }
            catch (Exception ex)
            {
                return new Dictionary<string, object> { ["available"] = false, ["error"] = ex.Message };
            }
        }

        // ─── Media Library ────────────────────────────────────────

        private Dictionary<string, object> GetMediaLibraryHealth()
        {
            try
            {
                var masterDb = Sitecore.Configuration.Factory.GetDatabase("master");
                var mediaRoot = masterDb?.GetItem("/sitecore/media library");
                if (mediaRoot == null) return new Dictionary<string, object> { ["available"] = false };

                var allMedia = mediaRoot.Axes.GetDescendants();
                int imageCount = 0, documentCount = 0, videoCount = 0, otherCount = 0;

                foreach (var item in allMedia)
                {
                    var templateName = item.TemplateName.ToLowerInvariant();
                    if (templateName.Contains("image") || templateName.Contains("jpeg") || templateName.Contains("png"))
                        imageCount++;
                    else if (templateName.Contains("pdf") || templateName.Contains("doc"))
                        documentCount++;
                    else if (templateName.Contains("video") || templateName.Contains("mp4"))
                        videoCount++;
                    else
                        otherCount++;
                }

                return new Dictionary<string, object>
                {
                    ["available"] = true,
                    ["total_media"] = allMedia.Length,
                    ["images"] = imageCount,
                    ["documents"] = documentCount,
                    ["videos"] = videoCount,
                    ["other"] = otherCount,
                };
            }
            catch (Exception ex)
            {
                return new Dictionary<string, object> { ["available"] = false, ["error"] = ex.Message };
            }
        }

        // ─── Language Coverage ────────────────────────────────────

        private Dictionary<string, object> GetLanguageCoverage()
        {
            try
            {
                var masterDb = Sitecore.Configuration.Factory.GetDatabase("master");
                if (masterDb == null) return new Dictionary<string, object> { ["available"] = false };

                var languages = masterDb.GetLanguages();
                var coverage = new Dictionary<string, object>();

                var contentRoot = masterDb.GetItem("/sitecore/content");
                if (contentRoot == null) return new Dictionary<string, object> { ["available"] = false };

                var allContent = contentRoot.Axes.GetDescendants();
                int totalItems = allContent.Length;

                foreach (var lang in languages)
                {
                    int hasVersion = 0;
                    foreach (var item in allContent)
                    {
                        var versions = item.Versions.GetVersions(false, lang);
                        if (versions != null && versions.Length > 0)
                            hasVersion++;
                    }

                    coverage[lang.Name] = new Dictionary<string, object>
                    {
                        ["items_with_version"] = hasVersion,
                        ["total_items"] = totalItems,
                        ["coverage_pct"] = totalItems > 0 ? Math.Round((hasVersion / (double)totalItems) * 100, 1) : 0
                    };
                }

                return new Dictionary<string, object>
                {
                    ["available"] = true,
                    ["language_count"] = languages.Length,
                    ["coverage"] = coverage
                };
            }
            catch (Exception ex)
            {
                return new Dictionary<string, object> { ["available"] = false, ["error"] = ex.Message };
            }
        }
    }
}
