using Microsoft.Owin;
using Owin;

[assembly: OwinStartupAttribute(typeof(PowerBIWebApp.Startup))]
namespace PowerBIWebApp
{
    public partial class Startup
    {
        public void Configuration(IAppBuilder app)
        {
            ConfigureAuth(app);
            app.MapSignalR();
        }
    }
}
