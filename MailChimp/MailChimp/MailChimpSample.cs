using MailChimp.Net;
using MailChimp.Net.Core;
using MailChimp.Net.Interfaces;
using MailChimp.Net.Models;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace MailChimp
{
    public interface IMailChimpSample
    {
        void DeleteEmailList(string listId);
        string CreateEmailList(int campaignId, string name, Contact mailChimpContact, CampaignDefaults mailChimpCampaignDefaults);
        void AddMembersToEmailList(int campaignId, string listId, IEnumerable<string> recipients);
        void DeleteEmailTemplate(int templateId);
        int CreateEmailTemplate(int campaignId, string name, string folderId, string body);
        void DeleteEmailCampaign(string campaignId);
        string CreateEmailCampaign(int campaignId, string listId, string fromName, string subjectLine, string replyTo, int templateId);
        void SendEmailCampaign(string campaignId);
        bool IsCampaignReadyToSend(string campaignId);
        IEnumerable<Campaign> GetCampaigns();
        IEnumerable<List> GetLists();
        string GetEmailCampaignDeliveryStatus(string campaignId);
        CampaignDefaults GetCampaignDefaults(string fromEmail, string fromName, string language, string subject);
        List GetList(string listId);
        IEnumerable<Template> GetEmailTemplates();
    }

    public class MailChimpSample : IMailChimpSample
    {
        private readonly IMailChimpManager manager;

        public MailChimpSample(string apiKey)
        {
            manager = new MailChimpManager(apiKey);
        }

        public CampaignDefaults GetCampaignDefaults(string fromEmail, string fromName, string language, string subject)
        {
            return new CampaignDefaults
            {
                FromEmail = fromEmail,
                FromName = fromName,
                Language = language,
                Subject = subject,
            };
        }

        public void DeleteEmailList(string listId)
        {
            manager.Lists.DeleteAsync(listId).Wait();
        }

        public string CreateEmailList(int campaignId, string name, Contact contact, CampaignDefaults mailChimpCampaignDefaults)
        {
            var list = new List();
            list.Name = name;
            list.PermissionReminder = "{PermissionReminder}";
            list.Contact = contact;

            list.CampaignDefaults = mailChimpCampaignDefaults;

            // create list
            var listId = manager.Lists.AddOrUpdateAsync(list).Result.Id;

            return listId;
        }

        public void AddMembersToEmailList(int campaignId, string listId, IEnumerable<string> recipients)
        {
            var members = new List<Member>();

            // add subscribers to list
            foreach (var recipient in recipients)
            {
                members.Add(new Member
                {
                    EmailAddress = recipient,
                    StatusIfNew = Status.Subscribed,
                });
            }

            var batchRequest = new BatchRequest
            {
                Operations = members.Select(x => new Operation
                {
                    Method = "PUT",
                    Path = $"/lists/{listId}/members/{manager.Members.Hash(x.EmailAddress.ToLower())}",
                    Body = JsonConvert.SerializeObject(
                        x,
                        new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore }),
                }),
            };

            var batch = manager.Batches.AddAsync(batchRequest).Result;
            var batchId = batch.Id;
            while (batch.Status != "finished")
            {
                batch = manager.Batches.GetBatchStatus(batchId).Result;
                Task.Delay(new TimeSpan(0, 0, 30)).Wait();
            }

            if (batch.ErroredOperations > 0)
            {
                Console.WriteLine($"({campaignId})-Error adding members to list via batch for listId {listId}. Amount of errors: {batch.ErroredOperations}");
            }
        }

        public IEnumerable<Template> GetEmailTemplates()
        {
            var result = new List<Template>();
            for (var i = 0; i < 10; i++)
            {
                try
                {
                    result = manager.Templates.GetAllAsync().Result.ToList();
                    break;
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex);
                }
            }

            return result;
        }

        public void DeleteEmailTemplate(int templateId)
        {
            manager.Templates.DeleteAsync(templateId).Wait();
        }

        public int CreateEmailTemplate(int campaignId, string name, string folderId, string body)
        {
            var templateId = manager.Templates.CreateAsync(name, folderId, body).Result.Id;

            return templateId;
        }

        public void DeleteEmailCampaign(string campaignId)
        {
            manager.Campaigns.DeleteAsync(campaignId).Wait();
        }

        public string CreateEmailCampaign(int campaignId, string listId, string fromName, string subjectLine, string replyTo, int templateId)
        {
            var campaignRequest = new Campaign();

            var recipients = new Recipient();
            recipients.ListId = listId;

            campaignRequest.Recipients = recipients; // add recipients

            var settings = new Setting();

            settings.FromName = fromName;
            settings.SubjectLine = subjectLine;
            settings.ReplyTo = replyTo;
            settings.TemplateId = templateId; // add template id

            campaignRequest.Settings = settings;

            campaignRequest.Type = CampaignType.Regular;

            var createdCampaign = manager.Campaigns.AddAsync(campaignRequest).Result;

            return createdCampaign.Id;
        }

        public void SendEmailCampaign(string campaignId)
        {
            int maxTries = 0;
            while (!IsCampaignReadyToSend(campaignId) && maxTries < 10) // try it 10 times
            {
                Task.Delay(new TimeSpan(0, 1, 0)).Wait(); // once per minute
                maxTries++;
            }

            manager.Campaigns.SendAsync(campaignId).Wait();
        }

        public bool IsCampaignReadyToSend(string campaignId)
        {
            var result = manager.Campaigns.SendChecklistAsync(campaignId).Result.IsReady;

            return result;
        }

        public IEnumerable<Member> GetAllListMembers()
        {
            var result = new List<Member>();

            var lists = GetLists();

            foreach (var list in lists)
            {
                try
                {
                    var campaign = manager.Campaigns.GetAllAsync(new CampaignRequest
                    {
                        ListId = list.Id
                    }).Result.FirstOrDefault();

                    if (campaign == null)
                    {
                        continue;
                    }

                    var members = manager.Members.GetAllAsync(list.Id).Result;
                    result.AddRange(members);
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex); // keep trying the others
                }
            }

            return result;
        }

        public IEnumerable<Campaign> GetCampaigns()
        {
            var result = new List<Campaign>();
            for (var i = 0; i < 10; i++)
            {
                try
                {
                    result = manager.Campaigns.GetAllAsync().Result.ToList();
                    break;
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex);
                }
            }

            return result;
        }

        public IEnumerable<List> GetLists()
        {
            var result = new List<List>();
            for (var i = 0; i < 10; i++)
            {
                try
                {
                    result = manager.Lists.GetAllAsync().Result.ToList();
                    break;
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex);
                }
            }

            return result;
        }

        public List GetList(string listId)
        {
            try
            {
                return manager.Lists.GetAsync(listId).Result;
            }
            catch // swallow exception, list doesn't exist, return null
            {
                return null;
            }
        }

        public string GetEmailCampaignDeliveryStatus(string campaignId)
        {
            var result = manager.Campaigns.GetAsync(campaignId).Result;

            var status = result.Status;

            return status;
        }
    }
}
