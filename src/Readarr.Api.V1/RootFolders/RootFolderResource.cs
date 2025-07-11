using System;
using System.Collections.Generic;
using System.Linq;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.Books;
using NzbDrone.Core.Books.Calibre;
using NzbDrone.Core.RootFolders;
using Readarr.Http.REST;

namespace Readarr.Api.V1.RootFolders
{
    public class RootFolderResource : RestResource
    {
        public string Name { get; set; }
        public string Path { get; set; }
        public int DefaultMetadataProfileId { get; set; }
        public int DefaultQualityProfileId { get; set; }
        public MonitorTypes DefaultMonitorOption { get; set; }
        public NewItemMonitorTypes DefaultNewItemMonitorOption { get; set; }
        public HashSet<int> DefaultTags { get; set; }
        public bool IsCalibreLibrary { get; set; }
        public string Host { get; set; }
        public int Port { get; set; }
        public string UrlBase { get; set; }
        public string Username { get; set; }
        public string Password { get; set; }
        public string Library { get; set; }
        public string OutputFormat { get; set; }
        public string OutputProfile { get; set; }
        public bool UseSsl { get; set; }

        public bool Accessible { get; set; }
        public long? FreeSpace { get; set; }
        public long? TotalSpace { get; set; }
    }

    public static class RootFolderResourceMapper
    {
        public static RootFolderResource ToResource(this RootFolder model)
        {
            if (model == null)
            {
                return null;
            }

            return new RootFolderResource
            {
                Id = model.Id,

                Name = model.Name,
                Path = model.Path.GetCleanPath(),
                DefaultMetadataProfileId = model.DefaultMetadataProfileId,
                DefaultQualityProfileId = model.DefaultQualityProfileId,
                DefaultMonitorOption = model.DefaultMonitorOption,
                DefaultNewItemMonitorOption = model.DefaultNewItemMonitorOption,
                DefaultTags = model.DefaultTags,
                IsCalibreLibrary = model.IsCalibreLibrary,
                Host = model.CalibreSettings?.Host,
                Port = model.CalibreSettings?.Port ?? 0,
                UrlBase = model.CalibreSettings?.UrlBase,
                Username = model.CalibreSettings?.Username,
                Password = model.CalibreSettings?.Password,
                Library = model.CalibreSettings?.Library,
                OutputFormat = model.CalibreSettings?.OutputFormat,
                OutputProfile = ((CalibreProfile)(model.CalibreSettings?.OutputProfile ?? 0)).ToString(),
                UseSsl = model.CalibreSettings?.UseSsl ?? false,

                Accessible = model.Accessible,
                FreeSpace = model.FreeSpace,
                TotalSpace = model.TotalSpace,
            };
        }

        public static RootFolder ToModel(this RootFolderResource resource)
        {
            if (resource == null)
            {
                return null;
            }

            CalibreSettings cs;
            if (resource.IsCalibreLibrary)
            {
                cs = new CalibreSettings
                {
                    Host = resource.Host,
                    Port = resource.Port,
                    UrlBase = resource.UrlBase,
                    Username = resource.Username,
                    Password = resource.Password,
                    Library = resource.Library,
                    OutputFormat = resource.OutputFormat,
                    OutputProfile = (int)Enum.Parse(typeof(CalibreProfile), resource.OutputProfile, true),
                    UseSsl = resource.UseSsl
                };
            }
            else
            {
                cs = null;
            }

            return new RootFolder
            {
                Id = resource.Id,
                Name = resource.Name,
                Path = resource.Path,

                DefaultMetadataProfileId = resource.DefaultMetadataProfileId,
                DefaultQualityProfileId = resource.DefaultQualityProfileId,
                DefaultMonitorOption = resource.DefaultMonitorOption,
                DefaultNewItemMonitorOption = resource.DefaultNewItemMonitorOption,
                DefaultTags = resource.DefaultTags ?? new HashSet<int>(),
                IsCalibreLibrary = resource.IsCalibreLibrary,
                CalibreSettings = cs
            };
        }

        public static List<RootFolderResource> ToResource(this IEnumerable<RootFolder> models)
        {
            return models.Select(ToResource).ToList();
        }
    }
}
