namespace callRecords.Models
{
    /// <summary>
    /// Plan limit to Friendly Name Mapping POCO
    /// </summary>
    public class CallDetails
    {
        public PlanDetails planDetails { get; set; }
        public float callDurationTotal { get; set; }
        
    }
}