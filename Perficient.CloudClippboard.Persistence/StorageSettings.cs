using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.WindowsAzure;
using Microsoft.WindowsAzure.Storage;

namespace Perficient.CloudClippboard.Persistence
{
    static class StorageSettings
    {
        private static Lazy<CloudStorageAccount> _storageAccount = new Lazy<CloudStorageAccount>(() =>
        {
            string account = CloudConfigurationManager.GetSetting("StorageAccountName");
            // This enables the storage emulator when running locally using the Azure compute emulator.
            if (account == "{StorageAccountName}")
            {
                return CloudStorageAccount.DevelopmentStorageAccount;
            }

            string key = CloudConfigurationManager.GetSetting("StorageAccountAccessKey");
            string connectionString = String.Format("DefaultEndpointsProtocol=https;AccountName={0};AccountKey={1}", account, key);
            return CloudStorageAccount.Parse(connectionString);
        }, true);

        public static CloudStorageAccount StorageAccount
        {            
            get
            {
                return _storageAccount.Value;
            }
        }
    }
}
