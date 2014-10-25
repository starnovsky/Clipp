using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using Microsoft.WindowsAzure;

namespace Perficient.CloudClippboard.Common
{
    public static class SiteSettings
    {
        public static class Security
        {
            public static class Google
            {
                private static Lazy<string> _clientID = new Lazy<string>(() => { return CloudConfigurationManager.GetSetting("ClientID.Google"); }, true);
                private static Lazy<string> _clientSecret = new Lazy<string>(() => { return CloudConfigurationManager.GetSetting("ClientSecret.Google"); }, true);

                public static string ClientID
                {
                    get { return _clientID.Value; }
                }

                public static string ClientSecret
                {
                    get { return _clientSecret.Value; }
                }
            }
        }
    }
}