// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json;
using Microsoft.Identity.Client;
using callRecords.Models;
using callRecords.Extensions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Azure.WebJobs;
using System.Reflection;
using ExecutionContext = Microsoft.Azure.WebJobs.ExecutionContext;

namespace callRecords
{
    public class PSTNCallrecordPplanAlertsDailyTrigger
    {
        public static string baseFunctionsFolder = string.Empty;
        public static ILogger logger = null; 

        [FunctionName("CallrecordTrigger")]
        public async Task RunAsync([TimerTrigger("%CronTimerSchedule%")]TimerInfo myTimer, ILogger log, ExecutionContext executionContext)
        {
            log.LogInformation($"Getting Call Records for the current period.Executed at: {DateTime.Now}");
            
            // Set the baseFunctionsFolder variable to the path of the current function
            baseFunctionsFolder = executionContext.FunctionAppDirectory;
            logger = log;
        
            // Initialize Vars
            PlanDetails? planDetails = null;
            AuthenticationResult? result = null;
            var callLogRows = new PstnLogCallRows();
            var SendNotification = false;
            List<CallDetails> CallUsageTotals = new List<CallDetails>(16);

            // Initialize Configuration object
            var builder = new ConfigurationBuilder()
                .AddUserSecrets(Assembly.GetExecutingAssembly(),true)
                .AddEnvironmentVariables();
            var configurationRoot = builder.Build();
            var GenConfig = configurationRoot.Get<GENConfig>();

            // Start Authentication...
            string[] scopes = new string[] { "https://graph.microsoft.com/.default" };
            var app = ConfidentialClientApplicationBuilder.Create(GenConfig.ClientID)
                                                    .WithClientSecret(GenConfig.ClientSecret)
                                                    .WithAuthority(new Uri(string.Format("https://login.microsoftonline.com/{0}", GenConfig.TenantID)))
                                                    .Build();

            try
            {
                result = await app.AcquireTokenForClient(scopes).ExecuteAsync();
            }
            catch (MsalUiRequiredException ex)
            {
                logger.LogError(string.Format("{0}.Executed at: {1}",ex.Message, DateTime.Now));
            }

            if (result != null)
            {
                using (HttpClient httpClient = new HttpClient())
                {

                    // Look for records from the start of the period(first of the month) to the end of the current day (11:59:59 PM)
                    DateTime fromDateTime = new DateTime(DateTime.Now.Year,DateTime.Now.Month, 1); // Beginging of this month
                    DateTime toDateTime = new DateTime(DateTime.Now.Year,DateTime.Now.Month , DateTime.Now.Day,11,59,59); // End of Today
                    
                    // Initial MS Graph Uri for the "getPstnCalls" API
                    string url = string.Format("https://graph.microsoft.com/v1.0/communications/callRecords/getPstnCalls(fromDateTime={0},toDateTime={1})",fromDateTime.ToString("yyyy-MM-ddThh:mm:ss.sssZ"),toDateTime.ToString("yyyy-MM-ddThh:mm:ss.sssZ"));
                    
                    // Add Authorization Header
                    httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", result.AccessToken);
                    
                    do
                    {
                        try
                        {
                            // Get the Call Records via MS Graph API and Deserialize the JSON into a PstnLogCallRows object
                            var res = httpClient.GetAsync(url).Result;
                            var jsonString = res.Content.ReadAsStringAsync().Result.ToString();
                            callLogRows = JsonSerializer.Deserialize<PstnLogCallRows>(jsonString);
                            
                            // Check for nulls
                            if (callLogRows != null && callLogRows.pstnLogCallRow != null )
                            {
                                // Set the url for the next http call to get the next 1000 call records (API has an implicit $top=1000)
                                if (callLogRows.odatanextlink != null)
                                {
                                    url = callLogRows.odatanextlink;
                                }

                                // Fill the Plan UsageTotals Dictionary with the plan details
                                foreach (var call in callLogRows.pstnLogCallRow)
                                {
                                    #region Get Plan Details for Debugging 
                                    // Console.ForegroundColor = (ConsoleColor)(row++ % 14);
                                    // Console.WriteLine(string.Format("Called: {0:#-###-###-####} : for '{1}' minutes",Convert.ToInt64(call.calleeNumber),call.duration / 60));
                                    // Console.WriteLine(string.Format("charge: {0}",call.charge));
                                    // Console.WriteLine(string.Format("connectionCharge: {0}",call.connectionCharge));
                                    // Console.WriteLine(string.Format("currency: {0}",call.currency));
                                    // Console.WriteLine(string.Format("callType: {0}",call.callType));
                                    // Console.WriteLine(string.Format("startDateTime: {0}",call.startDateTime));
                                    // Console.WriteLine(string.Format("endDateTime: {0}",call.endDateTime));
                                    // Console.WriteLine(string.Format("userPrincipalName: {0}",call.userPrincipalName));
                                    // Console.WriteLine(string.Format("userDisplayName: {0}",call.userDisplayName));
                                    // Console.WriteLine(string.Format("userId: {0}",call.userId));
                                    // Console.WriteLine(string.Format("callId: {0}",call.callId));
                                    // Console.WriteLine(string.Format("id: {0}",call.id));
                                    // Console.WriteLine(string.Format("usageCountryCode: {0}",call.usageCountryCode));
                                    // Console.WriteLine(string.Format("tenantCountryCode: {0}",call.tenantCountryCode));
                                    // Console.WriteLine(string.Format("licenseCapability: {0}",call.licenseCapability));
                                    // Console.WriteLine(string.Format("destinationContext: {0}",call.destinationContext));
                                    #endregion
                                
                                    // We are only Concerned with outgoing calls...
                                    if (call.callType == "user_out")
                                    {
                                        // Get the Current Plan Details and Limits
                                        planDetails =  GetCurrentPlanTypeandLimits(call);
                                    
                                        // check if the current call type is in the PlanUsageTotals Dictionary
                                        // if it is, add the current call duration to the total
                                        // if it is not, add the current call type and duration to the dictionary

                                        if(CallUsageTotals.Any(p => p.planDetails.planTypeFriendlyName == planDetails.planTypeFriendlyName))
                                        {
                                            var currentCallDetails = CallUsageTotals.Where(p => p.planDetails.planTypeFriendlyName == planDetails.planTypeFriendlyName).FirstOrDefault();
                                            // Cumulative add the current call duration to the total
                                            if (currentCallDetails != null)
                                            {
                                                currentCallDetails.callDurationTotal += ((float)call.duration) / 60;
                                            }
                                            
                                        }
                                        else
                                        {
                                            CallUsageTotals.Add(new CallDetails 
                                                                {   planDetails = planDetails, 
                                                                    callDurationTotal = ((float)call.duration) / 60
                                                                });
                                        }
                                    }
                                }
                            }
                        }                            
                        catch (Exception ex)
                        {
                            log.LogError(string.Format("{0}.Executed at: {1}",ex.Message, DateTime.Now));
                            continue;
                        }
                    
                    } while(callLogRows != null && callLogRows.odatanextlink != null);

                    // Check if we need to send a notification
                    if (GenConfig.SendOnlyWhenThresholdExceeded)
                    {
                            // If we are over the threshold limit for any pool , then send a notification
                            if (CallUsageTotals.Any(p => (p.callDurationTotal / p.planDetails.planLimit) * 100 > GenConfig.ThresholdLimit))
                            {
                                log.LogWarning(string.Format("Threshold has exceeded at least one pool. Notifications are being sent. Executed at: {0}", DateTime.Now));
                                SendNotification = true;
                            }
                            else
                            {
                                log.LogInformation(string.Format("Threshold not exceeded for any pools. No Notification sent. Executed at: {0}", DateTime.Now));
                                return;
                            }
                    }
                    else
                    {
                        SendNotification = true;
                    }
                    
                    if (SendNotification)
                    {                    
                        // EXTENSIONS: Send the Notification based on configuration setting to Console or Teams...
                        // Send the Notification based on configuration setting to Console, Teams. or ALL...
                        switch (GenConfig.NotificationType)
                        {
                            case "Teams":
                                    TeamsNotification.SendAdaptiveCardWithTemplating(CallUsageTotals,GenConfig,log).ConfigureAwait(false).GetAwaiter().GetResult();
                                    break; 
                            case "Console":
                                    ConsoleNotification.WriteToConsole(CallUsageTotals,GenConfig,log).ConfigureAwait(false).GetAwaiter().GetResult();
                                    break;
                            case "ALL":
                                    TeamsNotification.SendAdaptiveCardWithTemplating(CallUsageTotals,GenConfig,log).ConfigureAwait(false).GetAwaiter().GetResult();
                                    ConsoleNotification.WriteToConsole(CallUsageTotals,GenConfig,log).ConfigureAwait(false).GetAwaiter().GetResult();
                                    break;
                        }
                    }
                }
            }
        }


    /// <summary>
    /// Get the Current Plan Type and Limits
    /// </summary>
    PlanDetails GetCurrentPlanTypeandLimits(PstnLogCallRow call)
    {
        
        bool InSelectCountriesFlag = false;
        bool callIsDomestic = false;
                
        // Check if call.usageCountryCode has one of these values: "UK","US","PR","CA"
        if (call.usageCountryCode == "UK" || call.usageCountryCode == "US" || call.usageCountryCode == "PR" || call.usageCountryCode == "CA")
                InSelectCountriesFlag = true;
        
        // Check if they are using the domestic or international Minutes
        if (call.destinationContext == "Domestic")
            callIsDomestic = true;
            
            // Get the limit for the plan buckets, The plan buckets are defined in the plans.json file
            // If we are missing a Plan Bucket definition, we will return a limit value of -1 and 
            // output a message to add it to the plans.json file
            KeyValuePair<string,int> currentCallTypePlanLimit = getPlanLimitByLicenseCapability(call.licenseCapability, callIsDomestic, InSelectCountriesFlag);

            // Return PLan Details
            return new PlanDetails { planTypeFriendlyName = currentCallTypePlanLimit.Key, planLimit = currentCallTypePlanLimit.Value};
    }

    /// <summary>
    /// Get the Plan Limit for the License Capability
    /// </summary>
    KeyValuePair<string,int> getPlanLimitByLicenseCapability(string licenseCapability, bool callIsDomestic, bool inSelectCountriesFlag)
    {
            // Deserialize plans.json to get the plan limits
            string plansFile = Path.Combine(baseFunctionsFolder, "plans.json");
            string plansJson = File.ReadAllText(plansFile);
            List<Plan> callingPlans = JsonSerializer.Deserialize<List<Plan>>(plansJson);
                        
            // linq query to get element in List<Plan> where Plan.LicenseCapability == licenseCapability
            Plan plan = callingPlans.Where(p => p.LicenseCapability == licenseCapability).FirstOrDefault();

            // If the plan is null, this means we do not have Plan limits configured for this LicenseCapability. Please Update the plans.json file
            if(plan == null)
            {
                plan = new Plan { LicenseCapability = licenseCapability, DOMESTIC_US_PR_CA_UK_OutBound_Limit = -1, DOMESTIC_Other_OutBound_Limit = -1, INTERNATIONAL_ALL_OutBound_Limit = -1 };
                logger.LogError(string.Format("Plan Limit not configured for LicenseCapability: {0}. Please Update the plans.json file",licenseCapability));
            }

            if (callIsDomestic)
            {
                if (inSelectCountriesFlag)
                    return new KeyValuePair<string, int>(string.Format("{0}_DOMESTIC_US_PR_CA_UK_OutBound_Type",plan.LicenseCapability), (int)plan.DOMESTIC_US_PR_CA_UK_OutBound_Limit);
                else
                    return new KeyValuePair<string, int>(string.Format("{0}_DOMESTIC_Other_OutBound_Type",plan.LicenseCapability), (int)plan.DOMESTIC_Other_OutBound_Limit);
            }
            else
            {
                return new KeyValuePair<string, int>(string.Format("{0}_INTERNATIONAL_ALL_OutBound_Type",plan.LicenseCapability), (int)plan.INTERNATIONAL_ALL_OutBound_Limit);
            }
                
    }
    }
}
