public class CompanyListing {


    public string Name { get; set; }
    public string PhoneNumber { get; set; }
    public string Email { get; set; }
    public string Address { get; set; }
    public string Website { get; set; }
    public string YellowPagesUrl { get; set; }

    public CompanyListing(string name, string phoneNumber, string email, string address, string website, string ypUrl)
    {
        Name = name;
        PhoneNumber = phoneNumber;
        Email = email;
        Address = address;
        Website = website;
        YellowPagesUrl = ypUrl;
    }

    public override string ToString()
    {
        return $"Name: {Name}\nPhone: {PhoneNumber}\nEmail: {Email}\nAddress: {Address}\nWebsite:{Website}\nYpUrl: {YellowPagesUrl}\n";
    }
}