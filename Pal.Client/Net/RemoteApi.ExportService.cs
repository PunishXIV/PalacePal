using Account;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Pal.Client.Net
{
    internal partial class RemoteApi
    {
        public async Task<(bool, ExportRoot)> DoExport(CancellationToken cancellationToken = default)
        {
            if (!await Connect(cancellationToken))
                return new(false, new());

            var exportClient = new ExportService.ExportServiceClient(_channel);
            var exportReply = await exportClient.ExportAsync(new ExportRequest
            {
                ServerUrl = RemoteUrl,
            }, headers: AuthorizedHeaders(), deadline: DateTime.UtcNow.AddSeconds(120), cancellationToken: cancellationToken);
            return (exportReply.Success, exportReply.Data);
        }
    }
}
