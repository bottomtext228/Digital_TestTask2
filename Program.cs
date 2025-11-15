using System.Globalization;
using System.Net;
using CsvHelper;
using CsvHelper.Configuration;



Console.WriteLine("Loading data...");

string filePath = "geo-US.csv";
var ranges = LoadDatabase(filePath);

Console.WriteLine("Data is loaded.\nEnter 'q' to exit.");

while (true)
{
    Console.WriteLine("Enter IP (IPv4 or IPv6):");
    string input = Console.ReadLine()?.Trim() ?? "";

    if (input.Length == 1 && input.ToLower()[0] == 'q')
    {
        return;
    }

    if (IPAddress.TryParse(input, out var address))
    {
        var result = FindLocation(address, ranges);
        if (result != null)
        {
            Console.WriteLine($"Country: {result.CountryName} ({result.CountryCode})\nState: {result.StateName} ({result.StateCode})");
        }
        else
        {
            Console.WriteLine("IP not found in database.");
        }
    }
    else
    {
        Console.WriteLine("Invalid IP format!");
    }
}


static List<IpRange> LoadDatabase(string filePath)
{
    var list = new List<IpRange>();

    var config = new CsvConfiguration(CultureInfo.InvariantCulture);

    using var reader = new StreamReader(filePath);
    using var csv = new CsvReader(reader, config);

    csv.Context.RegisterClassMap<CsvRowMap>();

    while (csv.Read())
    {
        CsvRow row;

        try
        {
            row = csv.GetRecord<CsvRow>();
        }
        catch
        {
            continue; // skip malformed line
        }

        // parse CIDR
        try
        {
            var (start, end) = IPAddressRange(row.Cidr);

            list.Add(new IpRange
            {
                Start = start,
                End = end,
                CountryCode = row.CountryCode,
                CountryName = row.CountryName,
                StateCode = row.StateCode,
                StateName = row.StateName
            });
        }
        catch
        {
            continue;
        }
    }

    list.Sort((a, b) => a.Start.CompareTo(b.Start));
    return list;
}

// Find location by using binary search
static IpRange? FindLocation(IPAddress address, List<IpRange> ranges)
{
    UInt128 ipValue = IpToInteger(address);

    int left = 0, right = ranges.Count - 1;
    while (left <= right)
    {
        int mid = left + ((right - left) >> 1);
        var current = ranges[mid];
        if (ipValue < current.Start)
            right = mid - 1;
        else if (ipValue > current.End)
            left = mid + 1;
        else
            return current;
    }

    return null;
}

static (UInt128 First, UInt128 Last) IPAddressRange(string cidr)
{
    var parts = cidr.Split('/');
    var address = IPAddress.Parse(parts[0]);
    int cidrBits = int.Parse(parts[1]);

    byte[] bytes = address.GetAddressBytes();
    int addrBits = bytes.Length * 8;

    UInt128 ipValue = 0;
    foreach (byte b in bytes)
    {
        ipValue = (ipValue << 8) + b;
    }

    // Compute mask sizes
    int hostBits = addrBits - cidrBits;

    // High mask keeps network prefix
    UInt128 highMask = hostBits == 128
        ? 0
        : ~((UInt128.One << hostBits) - 1);

    // Low mask sets host bits (broadcast end)
    UInt128 lowMask = (hostBits == 128)
        ? UInt128.MaxValue
        : (UInt128.One << hostBits) - 1;

    UInt128 first = ipValue & highMask;
    UInt128 last = ipValue | lowMask;

    return (first, last);
}

static UInt128 IpToInteger(IPAddress address)
{
    var bytes = address.GetAddressBytes();
    UInt128 ipValue = 0;
    foreach (byte b in bytes)
    {
        ipValue <<= 8;
        ipValue += b;
    }
    return ipValue;
}

public class IpRange
{
    public UInt128 Start { get; set; }
    public UInt128 End { get; set; }
    public string CountryCode { get; set; } = string.Empty;
    public string CountryName { get; set; } = string.Empty;
    public string StateCode { get; set; } = string.Empty;
    public string StateName { get; set; } = string.Empty;
}



public sealed class CsvRowMap : ClassMap<CsvRow>
{
    public CsvRowMap()
    {
        Map(m => m.Cidr).Index(0);
        Map(m => m.ContinentCode).Index(1);
        Map(m => m.ContinentName).Index(2);
        Map(m => m.CountryCode).Index(3);
        Map(m => m.CountryName).Index(4);
        Map(m => m.StateCode).Index(5);
        Map(m => m.StateName).Index(6);
    }
}

public class CsvRow
{
    public string Cidr { get; set; } = string.Empty;
    public string ContinentCode { get; set; } = string.Empty;
    public string ContinentName { get; set; } = string.Empty;
    public string CountryCode { get; set; } = string.Empty;
    public string CountryName { get; set; } = string.Empty;
    public string StateCode { get; set; } = string.Empty;
    public string StateName { get; set; } = string.Empty;
}
