using System;
using System.Net.Http;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using HtmlAgilityPack;

class Program
{
    private static readonly HttpClient client = new HttpClient();

    static async Task Main(string[] args)
    {
        await StartScrape();
    }

    static async Task StartScrape()
    {
        List<CompanyListing> companylistings = new List<CompanyListing>();
        // bool isFinishedPage = false;


        try
        {
            string responseBody = await GetWebpage();

            HtmlDocument htmlDoc = new HtmlDocument();
            htmlDoc.LoadHtml(responseBody);
            HtmlNode scrollNode = htmlDoc.DocumentNode.SelectSingleNode("//div[@class='scrollable-pane']");
            HtmlNode getShowCount = scrollNode.SelectSingleNode(".//span[contains(@class,'showing-count')]");

            String getCountText = getShowCount != null ? HtmlEntity.DeEntitize(getShowCount.InnerText.Trim()) : "N/A";
            String countText = StripTotalCountText(getCountText);
            int postCount =  int.Parse(countText);

            Console.WriteLine($"Total Count: {postCount}");
            var results = scrollNode.SelectNodes(".//div[contains(@class, 'result')]");

            if (results != null)
            {
                foreach (var result in results)
                {
                    var nameNode = result.SelectSingleNode(".//h2");
                    var numberNode = result.SelectSingleNode(".//div[contains(@class, 'phone') and contains(@class, 'primary')]");
                    var websiteNode = result.SelectSingleNode(".//a[contains(@class, 'track-visit-website')]");

                    var name = nameNode != null ? HtmlEntity.DeEntitize(nameNode.InnerText.Trim()) : "N/A";
                    string pattern = @"[\d\.]";
                    string nameClean = Regex.Replace(name, pattern, string.Empty);



                    var number = numberNode != null ? HtmlEntity.DeEntitize(numberNode.InnerText.Trim()) : "N/A";
                    var website = websiteNode != null ? HtmlEntity.DeEntitize(websiteNode.GetAttributeValue("href", "N/A")) : "N/A";
                    var email = "N/A";

                    CompanyListing company = new CompanyListing(nameClean.Trim(),number,email,website);
                    companylistings.Add(company);
  
                }

                foreach (var item in companylistings)
                {
                    Console.WriteLine($"Name: {item.Name}");
                    Console.WriteLine($"Phone: {item.PhoneNumber}");
                    Console.WriteLine($"Email: {item.Email}");
                    Console.WriteLine($"Website: {item.Website}");
                }

                // Set isFinishedPage to true or false based on your logic for ending the scraping
                // isFinishedPage = true;
            }
            else
            {
                Console.WriteLine("No results found.");
            }
        }
        catch (HttpRequestException e)
        {
            Console.WriteLine("\nException Caught!");
            Console.WriteLine("Message :{0} ", e.Message);
        }
    }

    static async Task<string> GetWebpage()
    {
        String searchTerm = "Electricians";
        String geoLocation = "Haines%20City%2C%20FL";
        int pageIndex = 1;
        String input = $"https://www.yellowpages.com/search?search_terms={searchTerm}&geo_location_terms={geoLocation}&page={pageIndex}";
        using HttpResponseMessage response = await client.GetAsync(input);
        response.EnsureSuccessStatusCode();
        string responseBody = await response.Content.ReadAsStringAsync();
        return responseBody;
    }

    public static string StripTotalCountText(string input)
    {
        string pattern = @"of (\d+)";

        Match match = Regex.Match(input, pattern);

        return (match.Success) ? match.Groups[1].Value : "";
    }
}
