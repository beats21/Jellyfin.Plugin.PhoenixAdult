using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Common.Net;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Providers;
using PhoenixAdult.Helpers.Utils;

#if __EMBY__
using MediaBrowser.Model.Configuration;
#else
#endif

namespace PhoenixAdult
{
    public class ImageProvider : IRemoteImageProvider
    {
        public string Name => Plugin.Instance.Name;

        public bool Supports(BaseItem item) => item is Movie;

        public IEnumerable<ImageType> GetSupportedImages(BaseItem item) => new List<ImageType> {
                    ImageType.Primary,
                    ImageType.Backdrop
            };

#if __EMBY__
        public async Task<IEnumerable<RemoteImageInfo>> GetImages(BaseItem item, LibraryOptions libraryOptions, CancellationToken cancellationToken)
#else
        public async Task<IEnumerable<RemoteImageInfo>> GetImages(BaseItem item, CancellationToken cancellationToken)
#endif
        {
            var errorImages = new List<string>();
            var images = new List<RemoteImageInfo>();

            if (item == null)
                return images;

            if (!item.ProviderIds.TryGetValue(Name, out string externalID))
                return images;

            var curID = externalID.Split('#');
            if (curID.Length < 3)
                return images;

            var provider = ISiteList.GetProviderBySiteID(int.Parse(curID[0], CultureInfo.InvariantCulture));
            if (provider != null)
            {
                images = (List<RemoteImageInfo>)await provider.GetImages(item, cancellationToken).ConfigureAwait(false);

                var clearImages = new List<RemoteImageInfo>();
                foreach (var image in images)
                {
                    if (!clearImages.Where(o => o.Url == image.Url && o.Type == image.Type).Any() && !errorImages.Contains(image.Url, StringComparer.OrdinalIgnoreCase))
                    {
                        var imageDubl = clearImages.Where(o => o.Url == image.Url && o.Type != image.Type);
                        if (imageDubl.Any())
                        {
                            var t = imageDubl.First();
                            var img = new RemoteImageInfo
                            {
                                Url = t.Url,
                                ProviderName = t.ProviderName,
                                Height = t.Height,
                                Width = t.Width
                            };

                            if (t.Type == ImageType.Backdrop)
                            {
                                img.Type = ImageType.Primary;
                            }
                            else
                            {
                                if (t.Type == ImageType.Primary)
                                    img.Type = ImageType.Backdrop;
                            }

                            clearImages.Add(img);
                        }
                        else
                        {
                            var img = await ImageHelper.GetImageSizeAndValidate(image, cancellationToken).ConfigureAwait(false);
                            if (img != null)
                            {
                                image.ProviderName = Name;
                                image.Height = img.Height;
                                image.Width = img.Width;

                                clearImages.Add(image);
                            }
                            else
                            {
                                errorImages.Add(image.Url);
                            }
                        }
                    }

                    images = clearImages;
                }

                var backdrops = images.Where(o => o.Type == ImageType.Backdrop);
                if (backdrops.Any())
                {
                    var firstBackdrop = backdrops.First();
                    if (firstBackdrop != null && images.Where(o => o.Type == ImageType.Primary).First().Url == firstBackdrop.Url)
                    {
                        images.Remove(firstBackdrop);
                        images.Add(firstBackdrop);
                    }
                }
            }

            return images;
        }

        public Task<HttpResponseInfo> GetImageResponse(string url, CancellationToken cancellationToken) => Provider.Http.GetResponse(new HttpRequestOptions
        {
            CancellationToken = cancellationToken,
            Url = url,
            UserAgent = HTTP.GetUserAgent(),
        });
    }
}
