using System.Collections.Generic;
using NzbDrone.Core.Messaging.Commands;

namespace Readarr.Api.V1.BookFiles
{
    public class DeepIdentifyUnmappedFilesCommand : Command
    {
        public List<int> BookFileIds { get; set; }

        public override bool RequiresDiskAccess => true;
        public override bool IsLongRunning => true;
        public override string CompletionMessage => "Deep Identify Audio completed";
    }
}
