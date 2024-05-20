using System.Net.Sockets;
using System.Net;

namespace LiveboxExporter.Utility
{
    public sealed class IpAddressRange
    {
        // Src: https://stackoverflow.com/a/2138724/249742

        private static readonly IpAddressRange[] PrivateAddressRanges =
        [
            new IpAddressRange(IPAddress.Parse("10.0.0.0"), IPAddress.Parse("10.255.255.255")),
            new IpAddressRange(IPAddress.Parse("172.16.0.0"), IPAddress.Parse("172.31.255.255")),
            new IpAddressRange(IPAddress.Parse("192.168.0.0"), IPAddress.Parse("192.168.255.255")),
        ];

        readonly AddressFamily addressFamily;
        readonly byte[] lowerBytes;
        readonly byte[] upperBytes;

        public IpAddressRange(IPAddress lowerInclusive, IPAddress upperInclusive)
        {
            addressFamily = lowerInclusive.AddressFamily;
            lowerBytes = lowerInclusive.GetAddressBytes();
            upperBytes = upperInclusive.GetAddressBytes();
        }

        public static bool IsPrivateAddress(IPAddress address)
        {
            foreach (var range in PrivateAddressRanges)
            {
                if (range.IsInRange(address))
                    return true;
            }

            return false;
        }

        public bool IsInRange(IPAddress address)
        {
            if (address.AddressFamily != addressFamily)
            {
                return false;
            }

            byte[] addressBytes = address.GetAddressBytes();

            bool lowerBoundary = true, upperBoundary = true;

            for (int i = 0; i < lowerBytes.Length &&
                (lowerBoundary || upperBoundary); i++)
            {
                if (lowerBoundary && addressBytes[i] < lowerBytes[i] ||
                    upperBoundary && addressBytes[i] > upperBytes[i])
                {
                    return false;
                }

                lowerBoundary &= addressBytes[i] == lowerBytes[i];
                upperBoundary &= addressBytes[i] == upperBytes[i];
            }

            return true;
        }
    }

}
