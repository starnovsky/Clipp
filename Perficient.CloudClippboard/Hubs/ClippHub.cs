using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using Microsoft.AspNet.SignalR;
using Perficient.CloudClippboard.Logging;
using Perficient.CloudClippboard.Persistence;

namespace Perficient.CloudClippboard.Hubs
{
    public class ClippHub : Hub
    {
        public void SendText(string key, string text)
        {
            Clients.OthersInGroup(key).pushText(text);

            var service = new FileUploadService(new Logger());
            service.StoreText(key, text);
        }

        public void SetKey(string key)
        {
            Groups.Add(Context.ConnectionId, key);
        }
    }
}