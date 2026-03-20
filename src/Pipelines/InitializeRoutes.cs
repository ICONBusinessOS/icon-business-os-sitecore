using System.Web.Http;
using Sitecore.Pipelines;

namespace IconBusinessOS.Pipelines
{
    /// <summary>
    /// ICON BusinessOS — Route Registration Pipeline Processor.
    /// 
    /// Registers Web API routes for the silo API controller.
    /// Add to the initialize pipeline via config patch.
    /// </summary>
    public class InitializeRoutes
    {
        public void Process(PipelineArgs args)
        {
            GlobalConfiguration.Configure(config =>
            {
                config.Routes.MapHttpRoute(
                    name: "IconBusinessOS_Api",
                    routeTemplate: "api/icon/v1/{action}",
                    defaults: new
                    {
                        controller = "SiloApi",
                        action = RouteParameter.Optional
                    }
                );
            });
        }
    }
}
