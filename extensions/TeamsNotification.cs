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

    public static class TeamsNotification
{
        public static async Task SendAdaptiveCardWithTemplating(List<CallDetails> callDetails, GENConfig gENConfig, ILogger log)
        {
            string teamsNotificationCardFile = Path.Combine(PSTNCallrecordPplanAlertsDailyTrigger.baseFunctionsFolder, 
                                               "extensions", 
                                               "TeamsNotificationCard.json");
                                               
            var templateJson = File.ReadAllText(teamsNotificationCardFile);
            var template = new AdaptiveCardTemplate(templateJson);
            var cardJson = template.Expand(new { callDetails = callDetails, ThresholdLimit = gENConfig.ThresholdLimit });
            var card = AdaptiveCards.AdaptiveCard.FromJson(cardJson).Card;
            
            
            var attachment = new Attachment
            {
                ContentType = AdaptiveCard.ContentType,
                Content = card,
            };
            
            var message = new Activity()
            {
                Type = ActivityTypes.Message,
                Attachments = new List<Attachment>() { attachment },
                ChannelData = new TeamsChannelData()
                {
                    Notification = new NotificationInfo()
                    {
                        Alert = true
                    }
                }
            };
        
            var json = JsonConvert.SerializeObject(message);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            try
            {
            using (var HttpClient = new HttpClient())
            {
                var response = await HttpClient.PostAsync(gENConfig.TeamsWebHook, content).ConfigureAwait(false);

                if (!response.IsSuccessStatusCode)
                {
                    throw new Exception($"Failed to send message to Teams. Status code: {response.StatusCode}");
                } 

                log.LogInformation(string.Format("Message Sent to Teams Channel. Executed at: {0}", DateTime.Now));

            }
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to send message to Teams. Exception: {ex.Message}");
            }

   
        }

    }

}