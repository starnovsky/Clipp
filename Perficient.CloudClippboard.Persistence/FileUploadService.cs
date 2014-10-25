using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using Microsoft.WindowsAzure.Storage.File;
using Microsoft.WindowsAzure.Storage.RetryPolicies;
using Perficient.CloudClippboard.Entities;
using Perficient.CloudClippboard.Entities.Log;

namespace Perficient.CloudClippboard.Persistence
{
    public class FileUploadService
    {
        private const string FileContainerName = "files";
        private const string TextContainerName = "texts";

        private ILogger _log = null;

        public FileUploadService(ILogger logger)
        {
            _log = logger;
        }

        async public void CreateAndConfigureAsync()
        {
            try
            {
                CloudStorageAccount storageAccount = StorageSettings.StorageAccount;

                // Create a blob client and retrieve reference to images container
                CloudBlobClient blobClient = storageAccount.CreateCloudBlobClient();

                CloudBlobContainer container = blobClient.GetContainerReference(FileContainerName);

                // Create the "files" container if it doesn't already exist.
                if (await container.CreateIfNotExistsAsync())
                {
                    // Enable public access on the newly created "files" container
                    await container.SetPermissionsAsync(
                        new BlobContainerPermissions
                        {
                            PublicAccess =
                                BlobContainerPublicAccessType.Blob
                        });

                    _log.Information("Successfully created Blob Storage Files Container and made it public");
                }

                container = blobClient.GetContainerReference(TextContainerName);

                // Create the "texts" container if it doesn't already exist.
                if (await container.CreateIfNotExistsAsync())
                {
                    _log.Information("Successfully created Blob Storage Texts Container");
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, "Failure to Create or Configure files container in Blob Storage Service");
                throw;
            }
        }

        public string CreateBlockBlob(string key, string fileName)
        {
            CloudStorageAccount storageAccount = StorageSettings.StorageAccount;

            // Create the blob client and reference the container
            CloudBlobClient blobClient = storageAccount.CreateCloudBlobClient();
            CloudBlobContainer container = blobClient.GetContainerReference(FileContainerName);

            // Create a unique name for the images we are about to upload
            string imageName = String.Format("{0}.{1}.{2}",
                key,
                Guid.NewGuid().ToString(),
                Path.GetFileName(fileName));

            CloudBlockBlob blockBlob = container.GetBlockBlobReference(imageName);

            return imageName;
        }

        public async Task<string> UploadFileChunk(int id, string imageName, byte[] chunk)
        {
            string fullPath = null;
            Stopwatch timespan = Stopwatch.StartNew();

            try
            {
                CloudStorageAccount storageAccount = StorageSettings.StorageAccount;

                // Create the blob client and reference the container
                CloudBlobClient blobClient = storageAccount.CreateCloudBlobClient();
                CloudBlobContainer container = blobClient.GetContainerReference(FileContainerName);

                // Upload image to Blob Storage
                CloudBlockBlob blockBlob = container.GetBlockBlobReference(imageName);               
                
                var blockId = Convert.ToBase64String(Encoding.UTF8.GetBytes(
                        string.Format(CultureInfo.InvariantCulture, "{0:D4}", id)));
                
                using (var chunkStream = new MemoryStream(chunk))
                {
                    await blockBlob.PutBlockAsync(
                        blockId,
                        chunkStream, null, null,
                        new BlobRequestOptions()
                        {
                            RetryPolicy = new LinearRetry(TimeSpan.FromSeconds(10), 3)
                        },
                        null);
                }

                fullPath = blockBlob.Uri.ToString();

                timespan.Stop();
                _log.TraceApi("Blob Service", "FileUploadService.UploadFileChunkAsync", timespan.Elapsed, "imagepath={0}", fullPath);
                
            }
            catch (Exception ex)
            {
                _log.Error(ex, "Error upload photo blob to storage");
                throw;
            }

            return fullPath;
        }

        public async Task<string> CommintBlockList(IEnumerable<string> blockList, string imageName, string contentType)
        {
            string fullPath = null;
            try
            {
                CloudStorageAccount storageAccount = StorageSettings.StorageAccount;

                // Create the blob client and reference the container
                CloudBlobClient blobClient = storageAccount.CreateCloudBlobClient();
                CloudBlobContainer container = blobClient.GetContainerReference(FileContainerName);

                // Upload image to Blob Storage
                CloudBlockBlob blockBlob = container.GetBlockBlobReference(imageName);
                fullPath = blockBlob.Uri.ToString();

                blockBlob.Properties.ContentType = contentType;
                await blockBlob.PutBlockListAsync(blockList);
            }
            catch (Exception ex)
            {
                _log.Error(ex, "Error upload photo blob to storage");
                throw;
            }

            return fullPath;
        }

        public List<CloudFileModel> ListFiles(string key)
        {
            try
            {
                CloudStorageAccount storageAccount = StorageSettings.StorageAccount;

                // Create the blob client and reference the container
                CloudBlobClient blobClient = storageAccount.CreateCloudBlobClient();
                CloudBlobContainer container = blobClient.GetContainerReference(FileContainerName);

                return container.ListBlobs(key, false).Select(b => 
                    {
                        var blob = (CloudBlockBlob)b;
                        var nameParts = blob.Name.Split('.');
                        return new CloudFileModel()
                        {
                            FileName = string.Join(".", nameParts.Skip(2)),
                            ImageName = blob.Name,
                            Key = key,
                            Url = blob.Uri.ToString()
                        };
                    }).ToList();
            }
            catch (Exception ex)
            {
                _log.Error(ex, "Error listing files");
                throw;
            }
        }

        public async Task DeleteFile(string imageName)
        {
            try
            {
                CloudStorageAccount storageAccount = StorageSettings.StorageAccount;

                // Create the blob client and reference the container
                CloudBlobClient blobClient = storageAccount.CreateCloudBlobClient();
                CloudBlobContainer container = blobClient.GetContainerReference(FileContainerName);

                var blob = container.GetBlockBlobReference(imageName);
                await blob.DeleteIfExistsAsync();
            }
            catch (Exception ex)
            {
                _log.Error(ex, "Error deleting file");
                throw;
            }
        }

        public async Task CleanupOldBlobs(DateTimeOffset tresholdtime)
        {
            try
            {
                CloudStorageAccount storageAccount = StorageSettings.StorageAccount;

                // Create the blob client and reference the container
                CloudBlobClient blobClient = storageAccount.CreateCloudBlobClient();

                CloudBlobContainer container = blobClient.GetContainerReference(FileContainerName);
                await CleanupContainer(container, tresholdtime);

                container = blobClient.GetContainerReference(TextContainerName);
                await CleanupContainer(container, tresholdtime);
            }
            catch (Exception ex)
            {
                _log.Error(ex, "Error deleting old blobs");
                throw;
            }
        }

        private async Task CleanupContainer(CloudBlobContainer container, DateTimeOffset tresholdtime)
        {
            IEnumerable<IListBlobItem> blobs = container.ListBlobs(null, true, BlobListingDetails.None);

            foreach (IListBlobItem blob in blobs)
            {
                CloudBlockBlob block = (CloudBlockBlob)blob;
                try
                {
                    await block.DeleteIfExistsAsync(DeleteSnapshotsOption.None,
                        AccessCondition.GenerateIfNotModifiedSinceCondition(tresholdtime),
                        new BlobRequestOptions(), new OperationContext());
                }
                catch (StorageException ex)
                {
                    if (!ex.Message.Contains("(412)"))
                    {
                        throw;
                    }
                }
            }
        }

        public async Task StoreText(string key, string text)
        {
            try
            {
                CloudStorageAccount storageAccount = StorageSettings.StorageAccount;

                // Create the blob client and reference the container
                CloudBlobClient blobClient = storageAccount.CreateCloudBlobClient();
                CloudBlobContainer container = blobClient.GetContainerReference(TextContainerName);

                string blobName = key;
                CloudBlockBlob blockBlob = container.GetBlockBlobReference(blobName);
                blockBlob.Properties.ContentType = "text/plain";
                await blockBlob.UploadTextAsync(text);
            }
            catch (Exception ex)
            {
                _log.Error(ex, "Error storing text");
                throw;
            }
        }

        public async Task<string> RetrieveText(string key)
        {
            try
            {
                CloudStorageAccount storageAccount = StorageSettings.StorageAccount;

                // Create the blob client and reference the container
                CloudBlobClient blobClient = storageAccount.CreateCloudBlobClient();
                CloudBlobContainer container = blobClient.GetContainerReference(TextContainerName);

                string blobName = key;
                CloudBlockBlob blockBlob = container.GetBlockBlobReference(blobName);
                bool exist = await blockBlob.ExistsAsync();

                if (exist)
                {
                    try
                    {
                        return await blockBlob.DownloadTextAsync();
                    }
                    catch(StorageException ex)
                    {
                        if (!ex.Message.Contains("(404)"))
                        {
                            throw;
                        }
                    }
                }

                return null;
            }
            catch (Exception ex)
            {
                _log.Error(ex, "Error retrieving text");
                throw;
            }
        }

    }
}
