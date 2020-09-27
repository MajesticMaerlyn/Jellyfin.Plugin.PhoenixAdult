using System.Linq;
using System.Text;

namespace PhoenixAdult.Helpers
{
    internal static class PhoenixAdultHelper
    {
        public static string GetSearchSiteName(int[] siteNum)
        {
            if (siteNum == null)
                return null;

            return PhoenixAdultList.SiteList[siteNum[0]][siteNum[1]][0];
        }

        public static string GetSearchBaseURL(int[] siteNum)
        {
            if (siteNum == null)
                return null;

            if (!string.IsNullOrEmpty(PhoenixAdultList.SiteList[siteNum[0]][siteNum[1]].ElementAtOrDefault(1)))
                return PhoenixAdultList.SiteList[siteNum[0]][siteNum[1]][1];
            else
                return PhoenixAdultList.SiteList[siteNum[0]].First().Value[1];
        }

        public static string GetSearchSearchURL(int[] siteNum)
        {
            if (siteNum == null)
                return null;

            if (!string.IsNullOrEmpty(PhoenixAdultList.SiteList[siteNum[0]][siteNum[1]].ElementAtOrDefault(2)))
                return PhoenixAdultList.SiteList[siteNum[0]][siteNum[1]][2];
            else
                return PhoenixAdultList.SiteList[siteNum[0]].First().Value[2];
        }

        public static string Encode(string text)
            => Base58.EncodePlain(Encoding.UTF8.GetBytes(text));

        public static string Decode(string base64Text)
            => Encoding.UTF8.GetString(Base58.DecodePlain(base64Text));
    }
}
