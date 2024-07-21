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

    private static bool _isExportEnabled = false;
    private static bool _isEmailEnabled = false;

    static async Task Main(string[] args)
    {
        string searchTerm = GetInput("Enter search Term:");
        string geoLocation = GetInput("Enter Location:");

        _isEmailEnabled = GetYesOrNoInput("Do you want to include Emails in list?");
        _isExportEnabled = GetYesOrNoInput("Do you want the list exported to a CSV?");

        if (!string.IsNullOrWhiteSpace(searchTerm) && !string.IsNullOrWhiteSpace(geoLocation))
        {
            geoLocation.Replace(" ", "%20");
            await StartScrape(searchTerm, geoLocation);
            ExportToCsv("company_listings.csv");
        }
        else
        {
            Console.WriteLine("Can not search without searh term and geo location");
        }
    }

    static string GetInput(string prompt)
    {
        Console.WriteLine(prompt);
        return Console.ReadLine() ?? string.Empty;
    }

    static bool GetYesOrNoInput(string prompt)
    {
        Console.WriteLine($"{prompt} (y/n)");
        var response = Console.ReadLine()?.Trim().ToLower();
        return response == "y" || response == "yes";
    }

    static async Task StartScrape(string searchTerm, string geoLocation)
    {
        try
        {
            Console.WriteLine("\nStarting\n-------------------------------\n");

            int pageStart = 1;
            string responseBody = await GetWebpage(searchTerm, geoLocation, pageStart);

            HtmlDocument htmlDoc = new HtmlDocument();
            htmlDoc.LoadHtml(responseBody);
            HtmlNode scrollNode = htmlDoc.DocumentNode.SelectSingleNode("//div[@class='scrollable-pane']");


            HtmlNode getShowCount = scrollNode.SelectSingleNode(".//span[contains(@class,'showing-count')]");
            String getCountText = getShowCount != null ? HtmlEntity.DeEntitize(getShowCount.InnerText.Trim()) : "N/A";
            String countText = StripTotalCountText(getCountText);

            int postCount = int.Parse(countText);
            //Round up to get the last page post
  
            int pageCount = RoundUp(Math.Ceiling((postCount / 30.0) * 100) / 100,0);

            Console.WriteLine($"Total Count: {postCount}");
            Console.WriteLine($"Total Pages to Search: {pageCount}");
            Console.WriteLine("\n-------------------------------\n");
            Console.WriteLine("");

            var result = scrollNode.SelectNodes(".//div[contains(@class, 'result')]");

            await CreateCompanyListFromHtmlNode(result);
            //Start at page 2 because we have page 1
            for (var pageIndex = 2;pageIndex <= pageCount; pageIndex++)
            {
                Console.WriteLine($"pageIndex: {pageIndex}");
                string respBody = await GetWebpage(searchTerm, geoLocation, pageIndex);
                HtmlDocument htmlDocOther = new HtmlDocument();
                htmlDocOther.LoadHtml(respBody);
                HtmlNode scrollNodeOther = htmlDocOther.DocumentNode.SelectSingleNode("//div[@class='scrollable-pane']");
                var otherResult = scrollNodeOther.SelectNodes(".//div[contains(@class, 'result')]");
                await CreateCompanyListFromHtmlNode(otherResult);
            }

            if (!_isExportEnabled)
            {
                foreach (var item in _companyListings)
                {
                    Console.WriteLine(item.ToString());
                    Console.WriteLine("\n-------------------------------\n");
                }
            }
        }
        catch (HttpRequestException e)
        {
            Console.WriteLine("\nException Caught!");
            Console.WriteLine("Message :{0} ", e.Message);
        }
    }

    static async Task<string> GetCompanyWebpage(String route)
    {
        String input = $"https://www.yellowpages.com{route}";
        using HttpResponseMessage response = await _client.GetAsync(input);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync();
    }

    static async Task<string> GetWebpage(String searchTerm, String geoLocation, int pageIndex)
    {
        String input = $"https://www.yellowpages.com/search?search_terms={searchTerm}&geo_location_terms={geoLocation}&page={pageIndex}";
        using HttpResponseMessage response = await _client.GetAsync(input);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync();
    }

    public static string StripTotalCountText(string input)
    {
        string pattern = @"of (\d+)";
        Match match = Regex.Match(input, pattern);
        return match.Success ? match.Groups[1].Value : "";
    }

    static async Task CreateCompanyListFromHtmlNode(HtmlNodeCollection companyListings)
    {
        if (companyListings != null)
        {
            foreach (var companyListing in companyListings)
            {
                var nameNode = companyListing.SelectSingleNode(".//h2");
                var numberNode = companyListing.SelectSingleNode(".//div[contains(@class, 'phone') and contains(@class, 'primary')]");
                var addressNode = companyListing.SelectSingleNode(".//div[contains(@class, 'adr')]");
                var websiteNode = companyListing.SelectSingleNode(".//a[contains(@class, 'track-visit-website')]");
                var ypUrlNode = companyListing.SelectSingleNode(".//a[@href]");

                var name = nameNode != null ? HtmlEntity.DeEntitize(nameNode.InnerText.Trim()) : "N/A";
                string pattern = @"[\d\.]";
                string nameClean = Regex.Replace(name, pattern, string.Empty);

                if(nameClean.Trim() == "N/A") continue;

                var number = numberNode != null ? HtmlEntity.DeEntitize(numberNode.InnerText.Trim()) : "N/A";
                var address = addressNode != null ? HtmlEntity.DeEntitize(addressNode.InnerText.Trim()) : "N/A";
                var website = websiteNode != null ? HtmlEntity.DeEntitize(websiteNode.GetAttributeValue("href", "N/A")) : "N/A";
                var ypUrl = ypUrlNode != null ? ypUrlNode.GetAttributeValue("href", "N/A") : "N/A";
                var email = _isEmailEnabled ? "" : "N/A";
                

                if (ypUrl != "N/A" && _isEmailEnabled)
                {
                    try
                    {
                        string responseBody = await GetCompanyWebpage(ypUrl);
                        HtmlDocument htmlDoc = new HtmlDocument();
                        htmlDoc.LoadHtml(responseBody);
                        HtmlNode emailNode = htmlDoc.DocumentNode.SelectSingleNode(".//a[@class='email-business']");
                        string mailto = emailNode != null ? emailNode.GetAttributeValue("href", "N/A") : "N/A";
                        email = Regex.Replace(mailto, @"^mailto:", string.Empty);
                    }
                    catch (HttpRequestException e)
                    {
                        Console.WriteLine("\nException Caught!");
                        Console.WriteLine("Message :{0} ", e.Message);
                    }
                }

                CompanyListing company = new CompanyListing(nameClean.Trim(), number, email, address, website, ypUrl);
                _companyListings.Add(company);
            }

            _companyListings = _companyListings
            .Where(c => c.Name != "N/A")
            .OrderBy(c => c.Name)
            .ToList();

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

    public static int RoundUp(double value, int decimalPoint)
    {
        var result = Math.Round(value, decimalPoint);
        if (result < value)
        {
            result += Math.Pow(10, -decimalPoint);
        }
        Console.WriteLine($"Post Count Decemial: {result}");
        return (int)result;
    }
}