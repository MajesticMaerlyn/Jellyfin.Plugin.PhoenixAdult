using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Providers;
using PhoenixAdult.Configuration;
using PhoenixAdult.Helpers;
using PhoenixAdult.Helpers.Utils;

namespace PhoenixAdult.Sites
{
    public class NetworkR18 : IProviderBase
    {
        private static readonly IDictionary<string, string> CensoredWords = new Dictionary<string, string>
        {
            { "A*****t", "Assault" },
            { "A****p", "Asleep" },
            { "A***e", "Abuse" },
            { "B***d", "Blood" },
            { "B**d", "Bled" },
            { "C***d", "Child" },
            { "C*ck", "Cock" },
            { "D******e", "Disgrace" },
            { "D***king", "Drinking" },
            { "D***k", "Drunk" },
            { "D**g", "Drug" },
            { "F*****g", "Forcing" },
            { "F***e", "Force" },
            { "G*******g", "Gangbang" },
            { "G******g", "Gang Bang" },
            { "H*********n", "Humiliation" },
            { "H*******e", "Hypnotize" },
            { "H*******m", "Hypnotism" },
            { "H**t", "Hurt" },
            { "I****t", "Incest" },
            { "K****p", "Kidnap" },
            { "K****r", "Killer" },
            { "K**l", "Kill" },
            { "K*d", "Kid" },
            { "M************n", "Mother And Son" },
            { "M****t", "Molest" },
            { "P********t", "Passed Out" },
            { "P****h", "Punish" },
            { "R****g", "Raping" },
            { "R**e", "Rape" },
            { "RStepB****************r", "Stepbrother and Sister" },
            { "S*********l", "School Girl" },
            { "S********l", "Schoolgirl" },
            { "S******g", "Sleeping" },
            { "S*****t", "Student" },
            { "S***e", "Slave" },
            { "S**t", "Scat" },
            { "Sch**l", "School" },
            { "StepM************n", "Stepmother and Son" },
            { "T******e", "Tentacle" },
            { "T*****e", "Torture" },
            { "U*********s", "Unconscious" },
            { "V*****e", "Violate" },
            { "V*****t", "Violent" },
            { "Y********l", "Young Girl" },
        };

        public async Task<List<RemoteSearchResult>> Search(int[] siteNum, string searchTitle, DateTime? searchDate, CancellationToken cancellationToken)
        {
            var result = new List<RemoteSearchResult>();
            if (siteNum == null || string.IsNullOrEmpty(searchTitle))
            {
                return result;
            }

            string searchJAVID = null;
            var splitedTitle = searchTitle.Split();
            if (splitedTitle.Length > 1 && int.TryParse(splitedTitle[1], out _))
            {
                searchJAVID = $"{splitedTitle[0]}-{splitedTitle[1]}";
                searchTitle = searchJAVID;
            }

            var url = Helper.GetSearchSearchURL(siteNum) + searchTitle.Replace("-", " ", 1, StringComparison.OrdinalIgnoreCase);
            var data = await HTML.ElementFromURL(url, cancellationToken).ConfigureAwait(false);

            var searchResults = data.SelectNodes("//li[contains(@class, 'item-list')]");
            if (searchResults != null)
            {
                foreach (var searchResult in searchResults)
                {
                    string sceneURL = searchResult.SelectSingleNode(".//a").Attributes["href"].Value,
                            curID,
                            sceneName = Decensor(searchResult.SelectSingleNode(".//dt").InnerText),
                            scenePoster = searchResult.SelectSingleNode(".//img").Attributes["data-original"].Value,
                            javID = searchResult.SelectSingleNode(".//img").Attributes["alt"].Value;

                    sceneURL = sceneURL.Replace("/" + sceneURL.Split('/').Last(), string.Empty, StringComparison.OrdinalIgnoreCase);
                    curID = $"{siteNum[0]}#{siteNum[1]}#{Helper.Encode(sceneURL)}";

                    var res = new RemoteSearchResult
                    {
                        ProviderIds = { { Plugin.Instance.Name, curID } },
                        Name = $"{javID} {sceneName}",
                        ImageUrl = scenePoster,
                    };

                    if (!string.IsNullOrEmpty(searchJAVID))
                    {
                        res.IndexNumber = 100 - LevenshteinDistance.Calculate(searchJAVID, javID, StringComparison.OrdinalIgnoreCase);
                    }

                    result.Add(res);
                }
            }

            return result;
        }

        public async Task<MetadataResult<Movie>> Update(int[] siteNum, string[] sceneID, CancellationToken cancellationToken)
        {
            var result = new MetadataResult<Movie>()
            {
                Item = new Movie(),
                People = new List<PersonInfo>(),
            };

            if (sceneID == null)
            {
                return result;
            }

            var sceneURL = Helper.Decode(sceneID[0]);
            var sceneData = await HTML.ElementFromURL(sceneURL, cancellationToken).ConfigureAwait(false);

            result.Item.ExternalId = sceneURL;

            var javID = sceneData.SelectSingleText("//dt[text()='DVD ID:']/following-sibling::dd[1]");
            if (javID.StartsWith("--", StringComparison.OrdinalIgnoreCase))
            {
                javID = sceneData.SelectSingleText("//dt[text()='Content ID:']/following-sibling::dd[1]");
            }

            if (javID.Contains(" ", StringComparison.OrdinalIgnoreCase))
            {
                javID = javID.Replace(" ", "-", StringComparison.OrdinalIgnoreCase);
            }

            result.Item.OriginalTitle = javID.ToUpperInvariant();
            result.Item.Name = Decensor(sceneData.SelectSingleNode("//cite[@itemprop='name']").InnerText);

            var description = sceneData.SelectSingleNode("//div[@class='cmn-box-description01']");
            if (description != null)
            {
                var desc = description.InnerText.Replace("Product Description", string.Empty, StringComparison.OrdinalIgnoreCase);
                desc = Decensor(desc);

                result.Item.Overview = desc;
            }

            result.Item.AddStudio(sceneData.SelectSingleNode("//dd[@itemprop='productionCompany']").InnerText);

            var dateNode = sceneData.SelectSingleNode("//dd[@itemprop='dateCreated']");
            if (dateNode != null)
            {
                var date = dateNode.InnerText
                    .Replace(".", string.Empty, StringComparison.OrdinalIgnoreCase)
                    .Replace(",", string.Empty, StringComparison.OrdinalIgnoreCase)
                    .Replace("Sept", "Sep", StringComparison.OrdinalIgnoreCase)
                    .Replace("June", "Jun", StringComparison.OrdinalIgnoreCase)
                    .Replace("July", "Jul", StringComparison.OrdinalIgnoreCase)
                    .Trim();
                if (DateTime.TryParseExact(date, "MMM dd yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var sceneDateObj))
                {
                    result.Item.PremiereDate = sceneDateObj;
                }
            }

            var genreNode = sceneData.SelectNodes("//a[@itemprop='genre']");
            if (genreNode != null)
            {
                foreach (var genreLink in genreNode)
                {
                    var genreName = genreLink.InnerText;
                    genreName = Decensor(genreName);

                    result.Item.AddGenre(genreName);
                }
            }

            var actorsNode = sceneData.SelectNodes("//div[@itemprop='actors']//span[@itemprop='name']");
            if (actorsNode != null)
            {
                foreach (var actorLink in actorsNode)
                {
                    var actorName = actorLink.InnerText;

                    if (actorName != "----")
                    {
                        if (Plugin.Instance.Configuration.JAVActorNamingStyle == JAVActorNamingStyle.JapaneseStyle)
                        {
                            actorName = string.Join(" ", actorName.Split().Reverse());
                        }

                        var actor = new PersonInfo
                        {
                            Name = actorName,
                        };

                        var photoXpath = string.Format(CultureInfo.InvariantCulture, "//div[@id='{0}']//img[contains(@alt, '{1}')]", actorName.Replace(" ", string.Empty, StringComparison.OrdinalIgnoreCase), actorName);
                        var actorPhoto = sceneData.SelectSingleNode(photoXpath).Attributes["src"].Value;

                        if (!actorPhoto.Contains("nowprinting.gif", StringComparison.OrdinalIgnoreCase))
                        {
                            actor.ImageUrl = actorPhoto;
                        }

                        result.People.Add(actor);
                    }
                }
            }

            return result;
        }

        public async Task<IEnumerable<RemoteImageInfo>> GetImages(int[] siteNum, string[] sceneID, BaseItem item, CancellationToken cancellationToken)
        {
            var result = new List<RemoteImageInfo>();

            if (sceneID == null)
            {
                return result;
            }

            var sceneURL = Helper.Decode(sceneID[0]);
            var sceneData = await HTML.ElementFromURL(sceneURL, cancellationToken).ConfigureAwait(false);

            var img = sceneData.SelectSingleNode("//img[contains(@alt, 'cover')]").Attributes["src"].Value;
            result.Add(new RemoteImageInfo
            {
                Url = img,
                Type = ImageType.Primary,
            });

            foreach (var sceneImages in sceneData.SelectNodes("//section[@id='product-gallery']//img"))
            {
                result.Add(new RemoteImageInfo
                {
                    Url = sceneImages.Attributes["data-src"].Value,
                    Type = ImageType.Primary,
                });

                result.Add(new RemoteImageInfo
                {
                    Url = sceneImages.Attributes["data-src"].Value,
                    Type = ImageType.Backdrop,
                });
            }

            return result;
        }

        private static string Decensor(string text)
        {
            var result = text;

            foreach (var word in CensoredWords)
            {
                if (!result.Contains("*", StringComparison.OrdinalIgnoreCase))
                {
                    break;
                }

                result = result.Replace(word.Key, word.Value, StringComparison.OrdinalIgnoreCase);
            }

            return result;
        }
    }
}
