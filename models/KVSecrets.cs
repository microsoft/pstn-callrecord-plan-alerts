using System.Text.Json.Serialization;
namespace callRecords.Models
{
    /// <summary>
    /// KeyVault Secrets POCO
    /// </summary>
    public  class KVSecrets
    {
        
       public string TeamsWebHook {get; set; }
        
       public string ClientID { get; set; }
        
       public string ClientSecret { get; set; }
        
        public string TenantID {get; set; }
    }
}