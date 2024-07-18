using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using HtmlAgilityPack;
using CsvHelper;

class Program
{
    private static readonly HttpClient _client = new HttpClient();
    private static List<CompanyListing> _companyListings = new List<CompanyListing>();

    static async Task Main(string[] args)
    {
        await StartScrape();
        // ExportToCsv("company_listings.csv");
    }

    static async Task StartScrape()
    {
        String searchTerm = "Electricians";
        String geoLocation = "Haines%20City%2C%20FL";
        int pageIndex = 1;

        try
        {
            string responseBody = await GetWebpage(searchTerm, geoLocation, pageIndex);

            HtmlDocument htmlDoc = new HtmlDocument();
            htmlDoc.LoadHtml(responseBody);
            HtmlNode scrollNode = htmlDoc.DocumentNode.SelectSingleNode("//div[@class='scrollable-pane']");
            HtmlNode getShowCount = scrollNode.SelectSingleNode(".//span[contains(@class,'showing-count')]");

            HtmlNode getPagination = scrollNode.SelectSingleNode(".//div[contains(@class,'pagination')]");
            var paginations = getPagination.SelectNodes(".//li[span or a]");

            List<int> pageCount = new List<int>();

            if (paginations != null)
            {
                foreach (var pagination in paginations)
                {
                    HtmlNode spanNode = pagination.SelectSingleNode(".//span");
                    HtmlNode aNode = pagination.SelectSingleNode(".//a");

                    if (spanNode != null)
                    {
                        // Console.WriteLine("Found <span>: " + int.Parse(spanNode.InnerText));
                    }

                    if (aNode != null)
                    {
                        if (aNode.InnerText != "Next")
                        {
                            pageCount.Add(int.Parse(aNode.InnerText));
                            Console.WriteLine("Found <a>: " + int.Parse(aNode.InnerText));
                        }
                    }
                }
            }
            else
            {
                Console.WriteLine("No pagination <li> tags found.");
            }

            String getCountText = getShowCount != null ? HtmlEntity.DeEntitize(getShowCount.InnerText.Trim()) : "N/A";
            String countText = StripTotalCountText(getCountText);
            int postCount = int.Parse(countText);

            Console.WriteLine($"Total Count: {postCount}");

            var result = scrollNode.SelectNodes(".//div[contains(@class, 'result')]");

            CreateCompanyListFromHtmlNode(result);

            foreach (var pageId in pageCount)
            {
                pageIndex = pageId;
                string respBody = await GetWebpage(searchTerm, geoLocation, pageIndex);
                HtmlDocument htmlDocOther = new HtmlDocument();
                htmlDocOther.LoadHtml(respBody);
                HtmlNode scrollNodeOther = htmlDocOther.DocumentNode.SelectSingleNode("//div[@class='scrollable-pane']");
                var otherResult = scrollNodeOther.SelectNodes(".//div[contains(@class, 'result')]");
                CreateCompanyListFromHtmlNode(otherResult);
            }

            foreach (var item in _companyListings)
            {
                Console.WriteLine($"Name: {item.Name}");
                Console.WriteLine($"Phone: {item.PhoneNumber}");
                Console.WriteLine($"Email: {item.Email}");
                Console.WriteLine($"Website: {item.Website}");
                Console.WriteLine("-------------------------------");
                Console.WriteLine("");
            }
        }
        catch (HttpRequestException e)
        {
            Console.WriteLine("\nException Caught!");
            Console.WriteLine("Message :{0} ", e.Message);
        }
    }

    static async Task<string> GetWebpage(String searchTerm, String geoLocation, int pageIndex)
    {
        String input = $"https://www.yellowpages.com/search?search_terms={searchTerm}&geo_location_terms={geoLocation}&page={pageIndex}";
        using HttpResponseMessage response = await _client.GetAsync(input);
        response.EnsureSuccessStatusCode();
        string responseBody = await response.Content.ReadAsStringAsync();
        return responseBody;
    }

    public static string StripTotalCountText(string input)
    {
        string pattern = @"of (\d+)";
        Match match = Regex.Match(input, pattern);
        return match.Success ? match.Groups[1].Value : "";
    }

    public static void CreateCompanyListFromHtmlNode(HtmlNodeCollection companyListing)
    {
        if (companyListing != null)
        {
            foreach (var result in companyListing)
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

                CompanyListing company = new CompanyListing(nameClean.Trim(), number, email, website);

                _companyListings.Add(company);
            }
        }
        else
        {
            Console.WriteLine("No results found.");
        }
    }

    public static void ExportToCsv(string filePath)
    {
        using (var writer = new StreamWriter(filePath))
        using (var csv = new CsvWriter(writer, CultureInfo.InvariantCulture))
        {
            csv.WriteRecords(_companyListings);
        }
    }
}