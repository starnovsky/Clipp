using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace Perficient.CloudClippboard.Entities
{
    public class CloudFileModel
    {
        public string FileName { get; set; }
        public string Key { get; set; }
        public string ImageName { get; set; }
        public string Url { get; set; }
    }
}