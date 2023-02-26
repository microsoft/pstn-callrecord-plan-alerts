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
        public static async Task WriteToConsole(List<CallDetails> callDetails, GENConfig gENConfig, KVSecrets kvConfig,ILogger log)
        {
           int row = 1;
           
           foreach (var callUsageDetails in callDetails)
                {
                    Console.ForegroundColor = (ConsoleColor)(row++ % 14);
                    if (callUsageDetails.callDurationTotal > callUsageDetails.planDetails.planLimit)
                        log.LogInformation(string.Format("You are over the {0} limit of {1} minutes, with {2} minutes consumed for the period(month).", 
                            callUsageDetails.planDetails.planTypeFriendlyName, callUsageDetails.planDetails.planLimit,callUsageDetails.callDurationTotal));
                    else
                        log.LogInformation(string.Format("You are under the {0} limit of {1} minutes, with {2} minutes consumed for the period(month)."
                            , callUsageDetails.planDetails.planTypeFriendlyName, callUsageDetails.planDetails.planLimit,callUsageDetails.callDurationTotal));
                } 
        }

    }

}