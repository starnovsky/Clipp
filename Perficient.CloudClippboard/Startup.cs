using Microsoft.Owin;
using Owin;

[assembly: OwinStartupAttribute(typeof(Perficient.CloudClippboard.Startup))]
namespace Perficient.CloudClippboard
{
    public partial class Startup
    {
        public void Configuration(IAppBuilder app)
        {
            ConfigureAuth(app);
            ConfigureSignalR(app);
            ConfigureFileUploadService(app);
        }
    }
}
