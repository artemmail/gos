using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace Zakupki.Fetcher.Hubs;

[Authorize]
public sealed class NoticeAnalysisHub : Hub
{
}
