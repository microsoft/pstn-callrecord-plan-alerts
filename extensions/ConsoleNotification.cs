using callRecords.Models;
using AdaptiveCards;
using AdaptiveCards.Templating;
using Microsoft.Bot.Connector.Teams.Models;
using Microsoft.Bot.Schema;
using Newtonsoft.Json;
using System.Text;
using Microsoft.Extensions.Logging;

namespace callRecords.Extensions
{

    public static class ConsoleNotification
{
        public static async Task WriteToConsole(List<CallDetails> callDetails, GENConfig gENConfig,ILogger log)
        {
           int row = 1;
           
           foreach (var callUsageDetails in callDetails)
                {
                    Console.ForegroundColor = (ConsoleColor)(row++ % 14);

                    if ((callUsageDetails.callDurationTotal / callUsageDetails.planDetails.planLimit) * 100 > gENConfig.ThresholdLimit)
                        log.LogWarning(string.Format("You have hit the threshold of {0}% for {1} with a limit of {2} minutes, with {3} minutes consumed ({4:F1}%) for the period(month).", 
                            gENConfig.ThresholdLimit,
                            callUsageDetails.planDetails.planTypeFriendlyName, 
                            callUsageDetails.planDetails.planLimit,
                            callUsageDetails.callDurationTotal,
                            (float)(callUsageDetails.callDurationTotal / callUsageDetails.planDetails.planLimit) * 100));
                            
                    else
                        log.LogInformation(string.Format("You are under the threshold of {0}% for {1} with a limit of {2} minutes, with {3} minutes consumed ({4:F1}%) for the period(month).",
                            gENConfig.ThresholdLimit,
                            callUsageDetails.planDetails.planTypeFriendlyName, 
                            callUsageDetails.planDetails.planLimit,
                            callUsageDetails.callDurationTotal,
                            (float)(callUsageDetails.callDurationTotal / callUsageDetails.planDetails.planLimit) * 100));
                } 
        }

    }

}