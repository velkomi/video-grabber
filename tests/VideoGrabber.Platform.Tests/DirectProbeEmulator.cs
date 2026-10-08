using System.Net;
using System.Net.Http.Headers;
namespace VideoGrabber.Platform.Tests;
public sealed class DirectProbeEmulator : HttpMessageHandler
{
 protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
 {
  if(request.Method != HttpMethod.Head) throw new InvalidOperationException("Only metadata HEAD is allowed");
  if(request.RequestUri!.AbsolutePath == "/telegram-caption-origin.mp4")
   return Task.FromResult(new HttpResponseMessage(HttpStatusCode.Redirect)
   {
    Headers = { Location = new Uri("https://93.184.216.34/telegram-caption-transport.mp4?signature=synthetic-redirect-signature") }
   });
  var content = new ByteArrayContent([]);
  content.Headers.ContentLength = 1234;
  content.Headers.ContentType = new MediaTypeHeaderValue(request.RequestUri!.AbsolutePath.EndsWith(".mp4") ? "video/mp4" : "text/html");
  return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = content });
 }
}
