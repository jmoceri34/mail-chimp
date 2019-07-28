using System.Configuration;

namespace MailChimp
{
    class Program
    {
        static void Main(string[] args)
        {
            var apiKey = ConfigurationManager.AppSettings["MailChimpApiKey"];
            var mailChimpSample = new MailChimpSample(apiKey);
        }
    }
}
