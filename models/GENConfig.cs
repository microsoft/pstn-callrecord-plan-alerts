using System.Text.Json.Serialization;
namespace callRecords.Models
{
    /// <summary>
    /// MSAL Config POCO
    /// </summary>
    public  class GENConfig
    {
        public string KeyVaultName { get; set; }
        public string NotificationType { get; set; }
        public int ThresholdLimit { get; set; }
                
    }
}