using System;
using System.Threading.Tasks;
using Microsoft.Owin;
using Owin;
using Perficient.CloudClippboard.Logging;
using Perficient.CloudClippboard.Persistence;


namespace Perficient.CloudClippboard
{
    public partial class Startup
    {
        public void ConfigureFileUploadService(IAppBuilder app)
        {
            var service = new FileUploadService(new Logger());

            service.CreateAndConfigureAsync();

        }
    }
}
