using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using VideoGrabber.Platform.Contracts;
using VideoGrabber.Platform.Api.Telegram;
using Npgsql;
using Microsoft.Extensions.Configuration;
using VideoGrabber.Platform.Api.Jobs;
using Xunit;
namespace VideoGrabber.Platform.Tests;
public sealed class ClientOnlyMediaTests
{
 [Fact] public void Storage_defaults_to_client_only_and_blocks_media_transfer_paths()
 {
  var configuration = new ConfigurationBuilder().Build();
  Assert.True(MediaStoragePolicy.ClientOnly(configuration));
  Assert.True(MediaStoragePolicy.BlocksMediaPath("/v1/desktop-worker/devices/abc/uploads", "POST"));
  Assert.True(MediaStoragePolicy.BlocksMediaPath("/v1/downloads/signed-ticket", "GET"));
  Assert.False(MediaStoragePolicy.BlocksMediaPath("/download/windows", "GET"));
  Assert.False(MediaStoragePolicy.BlocksMediaPath("/v1/desktop-worker/devices/abc/complete-local", "POST"));
 }
 [Fact] public async Task Direct_probe_reads_only_headers_and_rejects_private_redirects()
 {
  var handler = new ProbeHandler();
  using var http = new HttpClient(handler);
  var resolver = new DirectMediaResolver(new EgressProxy(new PublicDns()), http);
  var result=await resolver.ResolveAsync(new Uri("https://media.example.test/video.mp4"), default);
  Assert.Equal("video/mp4",result.MediaType);
  Assert.Equal(HttpMethod.Head,handler.Method);
  Assert.Equal(0,handler.BodyReads);
  handler.Redirect=new Uri("http://127.0.0.1/private.mp4");
  await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>resolver.ResolveAsync(new Uri("https://media.example.test/video.mp4"),default));
 }

 [Fact] public async Task Client_only_rejects_server_jobs_and_uploads_without_reserving_credit()
 {
  await using var f=await ApiFixture.StartAsync();
  var account=await f.AccountAsync("email","media-guard",includeStarter:true);
  f.Service<IConfiguration>()["VG_MEDIA_STORAGE_MODE"]="client_only";
  var request=new CreateJob(Guid.NewGuid(),"irrelevant","download","server_worker",null,"src_test_source","best",[],null,null);
  using var job=await account.Client.PostAsJsonAsync("/v1/jobs",request);
  Assert.Equal(HttpStatusCode.Conflict,job.StatusCode);
  Assert.Contains("desktop_execution_required",await job.Content.ReadAsStringAsync());
  using var upload=await account.Client.PutAsync($"/v1/desktop-worker/devices/{Guid.NewGuid()}/uploads/{Guid.NewGuid()}",new ByteArrayContent(new byte[128]));
  Assert.Equal(HttpStatusCode.Conflict,upload.StatusCode);
  var access=await account.Client.GetFromJsonAsync<AccessSnapshot>("/v1/access");
  Assert.Equal(10,access!.RemainingDownloads);
 }
 [Fact] public async Task Direct_web_link_is_idempotent_charged_once_and_creates_no_media_artifact()
 {
  await using var f=await ApiFixture.StartAsync();
  var account=await f.AccountAsync("email","direct-link",includeStarter:true);
  var request=new DirectDownloadRequest(new Uri("https://93.184.216.34/video.mp4"),Guid.NewGuid());
  for(var i=0;i<2;i++)
  {
   using var response=await account.Client.PostAsJsonAsync("/v1/direct-downloads",request);
   response.EnsureSuccessStatusCode();
   var body=await response.Content.ReadFromJsonAsync<JsonElement>();
   Assert.Equal("link_issued",body.GetProperty("quotaAccounting").GetString());
  }
  var access=await account.Client.GetFromJsonAsync<AccessSnapshot>("/v1/access");
  Assert.Equal(9,access!.RemainingDownloads);
  await using var db=await f.Database.OpenConnectionAsync();
  await using var command=new NpgsqlCommand("select count(*) from licensing.artifacts",db);
  Assert.Equal(0L,await command.ExecuteScalarAsync());
 }
 [Fact] public async Task Telegram_receives_a_url_and_replay_never_sends_twice()
 {
  await using var f=await ApiFixture.StartAsync();
  var account=await f.AccountAsync("email","direct-telegram",includeStarter:true);
  var service=f.Service<DirectDownloadService>();var intent=Guid.NewGuid();
  Assert.Equal("delivered",await service.SendTelegramAsync(account.Id,new Uri("https://93.184.216.34/video.mp4"),intent,9123,default));
  Assert.Equal("already_delivered",await service.SendTelegramAsync(account.Id,new Uri("https://93.184.216.34/video.mp4"),intent,9123,default));
  var sent=Assert.Single(f.TelegramApi.Requests,x=>x.Path.EndsWith("/sendVideo"));
  var body=JsonSerializer.Deserialize<JsonElement>(sent.Body);
  Assert.Equal("https://93.184.216.34/video.mp4",body.GetProperty("video").GetString());
  Assert.DoesNotContain(f.TelegramApi.Requests,x=>x.Path.EndsWith("/sendDocument"));
 }
 [Fact] public async Task Lost_telegram_ack_is_review_required_and_is_not_automatically_retried()
 {
  await using var f=await ApiFixture.StartAsync();
  var account=await f.AccountAsync("email","direct-uncertain",includeStarter:true);
  f.TelegramApi.LoseNextVideoAck=true;
  var service=f.Service<DirectDownloadService>();var intent=Guid.NewGuid();
  Assert.Equal("review_required",await service.SendTelegramAsync(account.Id,new Uri("https://93.184.216.34/video.mp4"),intent,9123,default));
  Assert.Equal("review_required",await service.SendTelegramAsync(account.Id,new Uri("https://93.184.216.34/video.mp4"),intent,9123,default));
  Assert.Single(f.TelegramApi.Requests,x=>x.Path.EndsWith("/sendVideo"));
 }
 [Fact] public async Task Telegram_caption_uses_the_original_link_without_private_parameters_and_does_not_invent_metadata()
 {
  await using var f=await ApiFixture.StartAsync();
  var account=await f.AccountAsync("email","direct-caption",includeStarter:true);
  var source=new Uri("https://93.184.216.34/My%20video.mp4?token=synthetic-private-caption-token#private-fragment");
  Assert.Equal("delivered",await f.Service<DirectDownloadService>().SendTelegramAsync(account.Id,source,Guid.NewGuid(),9123,default));
  var sent=Assert.Single(f.TelegramApi.Requests,x=>x.Path.EndsWith("/sendVideo"));
  using var body=JsonDocument.Parse(sent.Body);
  var caption=body.RootElement.GetProperty("caption").GetString()!;
  Assert.Contains("My video",caption);
  Assert.Contains("https://93.184.216.34/My%20video.mp4",caption);
  Assert.DoesNotContain("synthetic-private-caption-token",caption);
  Assert.DoesNotContain("private-fragment",caption);
  Assert.Contains("Качество:",caption);
  Assert.Contains("Длительность:",caption);
  Assert.Contains("неизвест",caption,StringComparison.OrdinalIgnoreCase);
  Assert.True(caption.Length<=1024);
  Assert.False(body.RootElement.TryGetProperty("parse_mode",out _));
 }
 [Fact] public async Task Telegram_caption_keeps_its_limit_for_long_links()
 {
  await using var f=await ApiFixture.StartAsync();
  var account=await f.AccountAsync("email","direct-caption-long",includeStarter:true);
  var source=new Uri("https://93.184.216.34/"+new string('a',1800)+"/video.mp4?signature=synthetic-signature");
  Assert.Equal("delivered",await f.Service<DirectDownloadService>().SendTelegramAsync(account.Id,source,Guid.NewGuid(),9123,default));
  var sent=Assert.Single(f.TelegramApi.Requests,x=>x.Path.EndsWith("/sendVideo"));
  using var body=JsonDocument.Parse(sent.Body);
  var caption=body.RootElement.GetProperty("caption").GetString()!;
  Assert.True(caption.Length<=1024);
  Assert.Contains("Длительность:",caption);
  Assert.DoesNotContain("synthetic-signature",caption);
 }
 [Fact] public async Task Telegram_caption_does_not_expose_the_redirected_signed_transport_link()
 {
  await using var f=await ApiFixture.StartAsync();
  var account=await f.AccountAsync("email","direct-caption-redirect",includeStarter:true);
  var source=new Uri("https://93.184.216.34/telegram-caption-origin.mp4");
  Assert.Equal("delivered",await f.Service<DirectDownloadService>().SendTelegramAsync(account.Id,source,Guid.NewGuid(),9123,default));
  var sent=Assert.Single(f.TelegramApi.Requests,x=>x.Path.EndsWith("/sendVideo"));
  using var body=JsonDocument.Parse(sent.Body);
  Assert.Contains("telegram-caption-transport.mp4?signature=",body.RootElement.GetProperty("video").GetString());
  var caption=body.RootElement.GetProperty("caption").GetString()!;
  Assert.Contains(source.AbsoluteUri,caption);
  Assert.DoesNotContain("telegram-caption-transport",caption);
  Assert.DoesNotContain("synthetic-redirect-signature",caption);
 }
 [Fact] public async Task A_plain_link_downloads_in_private_bot_chat_once_without_opening_the_mini_app()
 {
  await using var f=await ApiFixture.StartAsync();
  _=await f.AccountAsync("telegram","918273647",includeStarter:true);
  f.Service<IConfiguration>()["VG_MEDIA_STORAGE_MODE"]="client_only";
  var update=new TelegramUpdate(921001,JsonSerializer.SerializeToElement(new
  {
   message=new { message_id=18L,from=new {id=918273647L},chat=new {id=918273647L,type="private"},text="https://93.184.216.34/video.mp4" }
  }));
  await f.Service<BotCommandHandler>().HandleAsync(update,default);
  await f.Service<BotCommandHandler>().HandleAsync(update,default);
  Assert.Single(f.TelegramApi.Requests,x=>x.Path.EndsWith("/sendVideo"));
  Assert.DoesNotContain(f.TelegramApi.Requests,x=>x.Path.EndsWith("/sendDocument"));
  Assert.DoesNotContain(f.TelegramApi.Requests,x=>x.Body.Contains("web_app",StringComparison.Ordinal));
  await using var db=await f.Database.OpenConnectionAsync();
  await using var command=new NpgsqlCommand("select count(*) from licensing.artifacts",db);
  Assert.Equal(0L,await command.ExecuteScalarAsync());
 }
 [Fact] public async Task Cancellation_after_confirmed_video_ack_does_not_turn_delivery_or_quota_into_unknown()
 {
  await using var f=await ApiFixture.StartAsync();
  var account=await f.AccountAsync("email","direct-ack-before-caption-cancel",includeStarter:true);
  using var original=new CancellationTokenSource();
  f.TelegramApi.DirectVideoMetadata=JsonSerializer.SerializeToElement(new {width=1280,height=720,duration=12});
  f.TelegramApi.CaptionEditStarted=original.Cancel;
  var downloads=f.Service<DirectDownloadService>();
  var intent=Guid.NewGuid();var source=new Uri("https://93.184.216.34/video.mp4");
  Assert.Equal("delivered",await downloads.SendTelegramAsync(account.Id,source,intent,9123,original.Token));
  Assert.True(original.IsCancellationRequested);
  Assert.Equal("already_delivered",await downloads.SendTelegramAsync(account.Id,source,intent,9123,default));
  Assert.Single(f.TelegramApi.Requests,x=>x.Path.EndsWith("/sendVideo"));
  Assert.Single(f.TelegramApi.Requests,x=>x.Path.EndsWith("/editMessageCaption"));
  var access=await account.Client.GetFromJsonAsync<AccessSnapshot>("/v1/access");
  Assert.Equal(9,access!.RemainingDownloads);
  await using var db=await f.Database.OpenConnectionAsync();
  await using var command=new NpgsqlCommand("select count(*) from licensing.artifacts",db);
  Assert.Equal(0L,await command.ExecuteScalarAsync());
 }
 [Fact] public async Task Explicit_telegram_link_keeps_web_and_windows_on_one_account_and_one_credit_balance()
 {
  await using var f=await ApiFixture.StartAsync();
  var web=await f.AccountAsync("email","unified-main",includeStarter:true);
  var windows=await f.AccountAsync("email","unified-main",includeStarter:true);
  Assert.Equal(web.Id,windows.Id);
  using var key=ECDsa.Create(ECCurve.NamedCurves.nistP256);
  using var device=await windows.Client.PostAsJsonAsync("/v1/devices",new DeviceRegistration("Test Windows","windows",Convert.ToBase64String(key.ExportSubjectPublicKeyInfo()),Guid.NewGuid()));
  device.EnsureSuccessStatusCode();
  const long telegramUser=918273645;
  var temporary=await f.AccountAsync("telegram",telegramUser.ToString(),includeStarter:true,telegramOnly:true);
  var ticket=await f.Service<TelegramAccountLinkService>().BeginAsync(temporary.Id,telegramUser,default);
  var token=Uri.UnescapeDataString(ticket.LinkUri.Query.TrimStart('?').Split('&').Single(x=>x.StartsWith("telegram_link=")).Split('=',2)[1]);
  using var linked=await web.Client.PostAsJsonAsync("/v1/telegram/account-link/complete",new CompleteTelegramAccountLinkRequest(token));
  linked.EnsureSuccessStatusCode();
  var resolved=await f.Service<ITelegramAccountResolver>().ResolveAsync(telegramUser,f.Clock.GetUtcNow(),default);
  Assert.Equal(web.Id,resolved);
  var profile=await windows.Client.GetFromJsonAsync<AccountProfile>("/v1/me");
  Assert.Contains("telegram",profile!.LinkedProviders);
  Assert.Single((await web.Client.GetFromJsonAsync<DeviceReceipt[]>("/v1/devices"))!);
  var access=await windows.Client.GetFromJsonAsync<AccessSnapshot>("/v1/access");
  Assert.Equal(10,access!.RemainingDownloads);
 }

 [Fact] public async Task Concurrent_telegram_replay_does_not_change_the_active_senders_reservation()
 {
  await using var f=await ApiFixture.StartAsync();var account=await f.AccountAsync("email","direct-concurrent",includeStarter:true);
  f.TelegramApi.DirectVideoEntered=new(TaskCreationOptions.RunContinuationsAsynchronously);
  f.TelegramApi.DirectVideoRelease=new(TaskCreationOptions.RunContinuationsAsynchronously);
  var service=f.Service<DirectDownloadService>();var intent=Guid.NewGuid();var url=new Uri("https://93.184.216.34/video.mp4");
  var original=service.SendTelegramAsync(account.Id,url,intent,9123,default);
  await f.TelegramApi.DirectVideoEntered.Task.WaitAsync(TimeSpan.FromSeconds(10));
  Assert.Equal("pending",await service.SendTelegramAsync(account.Id,url,intent,9123,default));
  f.TelegramApi.DirectVideoRelease.TrySetResult(true);
  Assert.Equal("delivered",await original);
  Assert.Single(f.TelegramApi.Requests,x=>x.Path.EndsWith("/sendVideo"));
  var access=await account.Client.GetFromJsonAsync<AccessSnapshot>("/v1/access");Assert.Equal(9,access!.RemainingDownloads);
 }
 [Fact] public async Task Cancelled_delivery_ack_is_recorded_with_an_independent_cleanup_token()
 {
  await using var f=await ApiFixture.StartAsync();var account=await f.AccountAsync("email","direct-aborted",includeStarter:true);
  f.TelegramApi.DirectVideoEntered=new(TaskCreationOptions.RunContinuationsAsynchronously);
  f.TelegramApi.DirectVideoRelease=new(TaskCreationOptions.RunContinuationsAsynchronously);
  var service=f.Service<DirectDownloadService>();var intent=Guid.NewGuid();var url=new Uri("https://93.184.216.34/video.mp4");
  using var cancelled=new CancellationTokenSource();
  var sending=service.SendTelegramAsync(account.Id,url,intent,9123,cancelled.Token);
  await f.TelegramApi.DirectVideoEntered.Task.WaitAsync(TimeSpan.FromSeconds(10));cancelled.Cancel();
  Assert.Equal("review_required",await sending);
  Assert.Equal("review_required",await service.SendTelegramAsync(account.Id,url,intent,9123,default));
  Assert.Single(f.TelegramApi.Requests,x=>x.Path.EndsWith("/sendVideo"));
 }
 [Fact] public async Task A_started_direct_send_hold_cannot_be_released_as_an_unstarted_job()
 {
  await using var f=await ApiFixture.StartAsync();var account=await f.AccountAsync("email","direct-started",includeStarter:true);
  var prepared=await f.Service<DirectDownloadService>().PrepareAsync(account.Id,new Uri("https://93.184.216.34/video.mp4"),Guid.NewGuid(),true,default,9123);
  Assert.False(await f.Service<VideoGrabber.Platform.Persistence.CreditLedger>().ReleaseUnstartedAsync(prepared.Receipt.ReservationId,default));
 }

 [Fact] public async Task Confirmed_direct_rejection_and_replay_use_one_lock_order_and_refund_the_hold()
 {
  await using var f=await ApiFixture.StartAsync();var account=await f.AccountAsync("email","direct-refund-race",includeStarter:true);
  var downloads=f.Service<DirectDownloadService>();var intent=Guid.NewGuid();var url=new Uri("https://93.184.216.34/video.mp4");
  var prepared=await downloads.PrepareAsync(account.Id,url,intent,true,default,9123);
  var replay=downloads.PrepareAsync(account.Id,url,intent,true,default,9123);
  var refund=downloads.ReleaseAsync(account.Id,prepared,default);
  await Task.WhenAll(replay,refund).WaitAsync(TimeSpan.FromSeconds(10));
  Assert.True(await refund);
  var access=await account.Client.GetFromJsonAsync<AccessSnapshot>("/v1/access");Assert.Equal(10,access!.RemainingDownloads);
 }
 private sealed class PublicDns:IDnsResolver { public Task<IPAddress[]> ResolveAsync(string host,CancellationToken token)=>Task.FromResult(new[]{IPAddress.Parse("93.184.216.34")}); }
 private sealed class ProbeHandler:HttpMessageHandler
 {
  public HttpMethod? Method;public Uri? Redirect;public int BodyReads;
  protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken token)
  {
   Method=request.Method;
   if(Redirect is not null){var response=new HttpResponseMessage(HttpStatusCode.Redirect);response.Headers.Location=Redirect;return Task.FromResult(response);}
   var content=new ProbeContent(()=>BodyReads++);
   content.Headers.ContentType=new("video/mp4");content.Headers.ContentLength=1234;
   return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK){Content=content});
  }
 }
 private sealed class ProbeContent(Action read):HttpContent
 {
  protected override Task SerializeToStreamAsync(Stream stream,TransportContext? context){read();throw new InvalidOperationException("Video bytes must not be read");}
  protected override bool TryComputeLength(out long length){length=1234;return true;}
 }
}
