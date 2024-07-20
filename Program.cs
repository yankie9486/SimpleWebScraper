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

    private static bool isEmailEnabled = false;

    static async Task Main(string[] args)
    {
        Console.WriteLine("Enter search Term:");
        string? searchTerm = Console.ReadLine();

        Console.WriteLine("Enter Location:");
        string geoLocation = Console.ReadLine().Replace(" ", "%20");

        Console.WriteLine("Do you want the list exported to a CSV?");
        Console.WriteLine("y/n");

        string? exportCSV = Console.ReadLine();

        if (!string.IsNullOrWhiteSpace(exportCSV))
        {
            if (exportCSV == "y" || exportCSV == "Y")
            {
                isEmailEnabled = true;
            }
            else if (exportCSV == "yes" || exportCSV == "Yes")
            {
                isEmailEnabled = true;
            }
            else
            {
                isEmailEnabled = false;
            }
        }
        else
        {
            isEmailEnabled = false;
        }

        if (!string.IsNullOrWhiteSpace(searchTerm) && !string.IsNullOrWhiteSpace(geoLocation))
        {
            await StartScrape(searchTerm, geoLocation);
        }
        else
        {
            Console.WriteLine("Can not search without searh term and geo location");
        }

        ExportToCsv("company_listings.csv");
    }

    static async Task StartScrape(string searchTerm, string geoLocation)
    {


        try
        {
            Console.WriteLine("");
            Console.WriteLine("Starting");
            Console.WriteLine("-------------------------------");
            Console.WriteLine("");
            int pageStart = 1;
            string responseBody = await GetWebpage(searchTerm, geoLocation, pageStart);

            HtmlDocument htmlDoc = new HtmlDocument();
            htmlDoc.LoadHtml(responseBody);
            HtmlNode scrollNode = htmlDoc.DocumentNode.SelectSingleNode("//div[@class='scrollable-pane']");
            HtmlNode getShowCount = scrollNode.SelectSingleNode(".//span[contains(@class,'showing-count')]");

            // HtmlNode getPagination = scrollNode.SelectSingleNode(".//div[contains(@class,'pagination')]");
            // var paginations = getPagination.SelectNodes(".//li[span or a]");

            // List<int> pageCount = new List<int>();

            // if (paginations != null)
            // {
            //     foreach (var pagination in paginations)
            //     {
            //         HtmlNode spanNode = pagination.SelectSingleNode(".//span");
            //         HtmlNode aNode = pagination.SelectSingleNode(".//a");

            //         if (spanNode != null)
            //         {
            //             //Not adding span because it has one and we have it.
            //         }

            //         if (aNode != null)
            //         {
            //             if (aNode.InnerText != "Next")
            //             {
            //                 pageCount.Add(int.Parse(aNode.InnerText));
            //             }
            //         }
            //     }
            // }
            // else
            // {
            //     Console.WriteLine("No pagination <li> tags found.");
            // }

            String getCountText = getShowCount != null ? HtmlEntity.DeEntitize(getShowCount.InnerText.Trim()) : "N/A";
            String countText = StripTotalCountText(getCountText);
            int postPerPage = 30;
            int postCount = int.Parse(countText);
            int pageCount = postCount/postPerPage;

            Console.WriteLine($"Total Count: {postCount}");
            Console.WriteLine($"Total Pages to Search: {pageCount}");
            Console.WriteLine("-------------------------------");
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

            if (!isEmailEnabled)
            {

                foreach (var item in _companyListings)
                {
                    Console.WriteLine(item.ToString());
                    Console.WriteLine("-------------------------------");
                    Console.WriteLine("");
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
        string responseBody = await response.Content.ReadAsStringAsync();
        return responseBody;
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

    static async Task CreateCompanyListFromHtmlNode(HtmlNodeCollection companyListing)
    {
        if (companyListing != null)
        {
            foreach (var result in companyListing)
            {
                var nameNode = result.SelectSingleNode(".//h2");
                var numberNode = result.SelectSingleNode(".//div[contains(@class, 'phone') and contains(@class, 'primary')]");
                var addressNode = result.SelectSingleNode(".//div[contains(@class, 'adr')]");
                var websiteNode = result.SelectSingleNode(".//a[contains(@class, 'track-visit-website')]");
                var ypUrlNode = result.SelectSingleNode(".//a[@href]");

                var name = nameNode != null ? HtmlEntity.DeEntitize(nameNode.InnerText.Trim()) : "N/A";
                string pattern = @"[\d\.]";
                string nameClean = Regex.Replace(name, pattern, string.Empty);

                if(nameClean.Trim() == "N/A")
                {
                    continue;
                }

                var number = numberNode != null ? HtmlEntity.DeEntitize(numberNode.InnerText.Trim()) : "N/A";
                var address = addressNode != null ? HtmlEntity.DeEntitize(addressNode.InnerText.Trim()) : "N/A";
                var website = websiteNode != null ? HtmlEntity.DeEntitize(websiteNode.GetAttributeValue("href", "N/A")) : "N/A";

                var ypUrl = ypUrlNode != null ? ypUrlNode.GetAttributeValue("href", "N/A") : "N/A";
                var email = "";
                isEmailEnabled = true;

                if (ypUrl != "N/A" && isEmailEnabled)
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
}