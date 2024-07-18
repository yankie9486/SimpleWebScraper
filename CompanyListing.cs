public class CompanyListing {


    public string Name { get; set; }
    public string PhoneNumber { get; set; }
    public string Email { get; set; }
    public string Website { get; set; }

    public CompanyListing(string name, string phoneNumber, string email, string website)
    {
        Name = name;
        PhoneNumber = phoneNumber;
        Email = email;
        Website = website;
    }

    public override string ToString()
    {
        return $"Name: {Name}\nPhone: {PhoneNumber}\nEmail: {Email}/nWebsite:{Website}/n/n";
    }
}