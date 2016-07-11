using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Internal;
using Microsoft.AspNetCore.Mvc.Razor;
using Microsoft.Extensions.DependencyInjection;

namespace Microsoft.AspNetCore.Builder
{
    public static class ApplicationBuilderExtensions
    {
        public static IMvcBuilder AddMvcPages(this IServiceCollection services)
        {
            return services.AddMvc();
        }

        public static IApplicationBuilder UseMvcPages(this IApplicationBuilder app)
        {
            var hostingEnv = app.ApplicationServices.GetRequiredService<IHostingEnvironment>();
            var viewEngine = app.ApplicationServices.GetRequiredService<IRazorViewEngine>() as RazorViewEngine;

            // Find the razor files & load them to force them to compile and get loaded into the app
            var razorFiles = Directory.GetFiles(hostingEnv.ContentRootPath, "*.cshtml");
            foreach (var path in razorFiles)
            {
                var view = viewEngine.GetView("", path, true);
                var page = viewEngine.GetPage("", path);
            }

            return app.UseMvc();
        }
    }
}
