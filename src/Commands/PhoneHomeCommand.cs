using System;
using Sitecore.Tasks;
using IconBusinessOS.Services;

namespace IconBusinessOS.Commands
{
    /// <summary>
    /// ICON BusinessOS — Sitecore Scheduled Task for Phone Home.
    /// 
    /// Configured in Sitecore as a scheduled task that runs every 15 minutes.
    /// Pushes composite health payload to fleet master.
    /// 
    /// Setup:
    ///   1. Create item under /sitecore/system/Tasks/Commands
    ///   2. Set Type: IconBusinessOS.Commands.PhoneHomeCommand, IconBusinessOS
    ///   3. Set Method: Execute
    ///   4. Create Schedule item under /sitecore/system/Tasks/Schedules
    ///   5. Set Command: the command item above
    ///   6. Set Schedule: every 15 minutes
    /// </summary>
    public class PhoneHomeCommand
    {
        public void Execute(Item[] items, CommandItem command, ScheduleItem schedule)
        {
            try
            {
                Sitecore.Diagnostics.Log.Info("[ICON BusinessOS] Phone home task started.", this);

                var systemMonitor = new SystemMonitor();
                var securityScanner = new SecurityScanner();
                var contentIntelligence = new ContentIntelligence();
                var phoneHome = new PhoneHome(systemMonitor, securityScanner, contentIntelligence);

                var result = phoneHome.ExecuteAsync().GetAwaiter().GetResult();

                if (result.ContainsKey("success") && (bool)result["success"])
                {
                    Sitecore.Diagnostics.Log.Info(
                        $"[ICON BusinessOS] Phone home successful. Score: {result["score"]}/100", this);
                }
                else
                {
                    var error = result.ContainsKey("error") ? result["error"].ToString() : "Unknown error";
                    Sitecore.Diagnostics.Log.Warn(
                        $"[ICON BusinessOS] Phone home failed: {error}", this);
                }
            }
            catch (Exception ex)
            {
                Sitecore.Diagnostics.Log.Error("[ICON BusinessOS] Phone home task error.", ex, this);
            }
        }
    }
}
