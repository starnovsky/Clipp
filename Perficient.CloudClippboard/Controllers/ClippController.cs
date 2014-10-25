using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;
using Microsoft.AspNet.SignalR;
using Perficient.CloudClippboard.Entities.Common;
using Perficient.CloudClippboard.Hubs;
using Perficient.CloudClippboard.Logging;
using Perficient.CloudClippboard.Models;
using Perficient.CloudClippboard.Persistence;
using Microsoft.AspNet.Identity.Owin;
using Microsoft.AspNet.Identity;

namespace Perficient.CloudClippboard.Controllers
{
    [SessionState(System.Web.SessionState.SessionStateBehavior.Disabled)]
    public class ClippController : Controller
    {
        // GET: Clipp
        public async Task<ActionResult> Index(string id)
        {
            UniqueKeyGenerator keygen = new UniqueKeyGenerator();

            if(Request.IsAuthenticated)
            {
                if(!string.IsNullOrWhiteSpace(id))
                {
                    RedirectToAction("Index");
                }

                var userManager = HttpContext.GetOwinContext().GetUserManager<ApplicationUserManager>();
                var user = await userManager.FindByIdAsync(User.Identity.GetUserId());
                id = user.ClippCode;

                // the following should never happen, but...
                if(string.IsNullOrWhiteSpace(id))
                {
                    id = keygen.GenerateKey();
                    user.ClippCode = id;
                    await userManager.UpdateAsync(user);
                }
            }

            if(string.IsNullOrWhiteSpace(id))
            {                
                return RedirectToAction("Index", new { id =  keygen.GenerateKey() });
            }

            var service = new FileUploadService(new Logger());
            var text = await service.RetrieveText(id);

            var model = new ClippViewModel()
            {
                UniqueKey = id,
                Text = text,
                IsAuthenticated = Request.IsAuthenticated
            };
            
            return View(model);
        }

        [HttpPost]
        public ActionResult SetMetadata(string key, string fileName)
        {
            var service = new FileUploadService(new Logger());

            string imageName = service.CreateBlockBlob(key, fileName);

            return Json(imageName);
        }

        [HttpPost]
        [ValidateInput(false)]
        public async Task<ActionResult> UploadChunk(string key, int id, string fileType, string imageName, int numberOfBlocks)
        {
            var service = new FileUploadService(new Logger());

            HttpPostedFileBase request = Request.Files["Slice"];
            byte[] chunk = new byte[request.ContentLength];
            request.InputStream.Read(chunk, 0, Convert.ToInt32(request.ContentLength));

            await service.UploadFileChunk(id, imageName, chunk);

            if (id == numberOfBlocks)
            {
                var blockList = Enumerable
                    .Range(1, numberOfBlocks)
                    .ToList<int>()
                    .ConvertAll(rangeElement =>
                            Convert.ToBase64String(Encoding.UTF8.GetBytes(
                            string.Format(CultureInfo.InvariantCulture, "{0:D4}", rangeElement))));

                await service.CommintBlockList(blockList, imageName, fileType);

                var context = GlobalHost.ConnectionManager.GetHubContext<ClippHub>();
                context.Clients.Group(key).notifyUploaded();

                return Json(new
                {
                    error = false,
                    isLastBlock = true,
                    message = "upload complete"
                });
            }

            return Json(new { error = false, isLastBlock = false, message = string.Empty });
        }

        [HttpPost]
        public ActionResult ListFiles(string key)
        {
            var service = new FileUploadService(new Logger());

            var files = service.ListFiles(key);

            return Json(files);
        }

        [HttpPost]
        public async Task<ActionResult> DeleteFile(string key, string imageName)
        {
            var service = new FileUploadService(new Logger());
            await service.DeleteFile(imageName);

            var context = GlobalHost.ConnectionManager.GetHubContext<ClippHub>();
            context.Clients.Group(key).notifyUploaded();

            return Json("success");
        }
    }
}