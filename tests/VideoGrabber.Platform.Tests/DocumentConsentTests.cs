using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using VideoGrabber.Platform.Api.ProductInformation;
using VideoGrabber.Platform.Contracts;
using VideoGrabber.Platform.Persistence;
using Xunit;

namespace VideoGrabber.Platform.Tests;

public sealed class DocumentConsentTests
{
    [Fact]
    public async Task Course_reservation_requires_explicit_rights_for_its_own_intent()
    {
        await using var f=await ApiFixture.StartAsync();using var catalog=new CatalogFixture();
        f.ProductDocumentsPath=catalog.ContentPath;await f.RestartAsync();
        var a=await f.AccountAsync("email","reservation-rights");
        using var key=System.Security.Cryptography.ECDsa.Create(System.Security.Cryptography.ECCurve.NamedCurves.nistP256);
        var deviceResponse=await a.Client.PostAsJsonAsync("/v1/devices",new DeviceRegistration("TEST","windows",Convert.ToBase64String(key.ExportSubjectPublicKeyInfo()),Guid.NewGuid()));
        deviceResponse.EnsureSuccessStatusCode();var device=(await deviceResponse.Content.ReadFromJsonAsync<DeviceReceipt>())!;
        var intent=Guid.NewGuid();var request=new ReservationRequest(intent,new string('a',64),"course_download","desktop_worker",device.DeviceId);
        var denied=await a.Client.PostAsJsonAsync("/v1/reservations",request);
        Assert.Equal(HttpStatusCode.Conflict,denied.StatusCode);
        Assert.Contains("content_rights_confirmation_required",await denied.Content.ReadAsStringAsync());
        var doc=catalog.Catalog.Read().First();
        await f.Service<ConsentStore>().RecordAsync(a.Id,new(doc.Id,doc.Version,doc.Sha256,"accepted","course_rights",Guid.NewGuid()),catalog.Catalog.Snapshot(doc.Id,doc.Sha256),default);
        denied=await a.Client.PostAsJsonAsync("/v1/reservations",request);
        Assert.Contains("content_rights_confirmation_required",await denied.Content.ReadAsStringAsync());
        await f.Service<ConsentStore>().RecordAsync(a.Id,new(doc.Id,doc.Version,doc.Sha256,"accepted","course_rights",intent),catalog.Catalog.Snapshot(doc.Id,doc.Sha256),default);
        var after=await a.Client.PostAsJsonAsync("/v1/reservations",request);
        Assert.DoesNotContain("content_rights_confirmation_required",await after.Content.ReadAsStringAsync());
    }
    [Fact]
    public void Formatting_and_line_endings_do_not_change_document_identity()
    {
        using var f=new CatalogFixture();var first=f.Catalog.Read().First();var snapshot=f.Catalog.Snapshot(first.Id,first.Sha256);
        using var parsed=JsonDocument.Parse(File.ReadAllText(f.ContentPath));
        File.WriteAllText(f.ContentPath,JsonSerializer.Serialize(parsed.RootElement,new JsonSerializerOptions{WriteIndented=true}).Replace("\n","\r\n"));
        var next=f.Catalog.Read().First();Assert.Equal(first.Sha256,next.Sha256);Assert.Equal(snapshot,f.Catalog.Snapshot(next.Id,next.Sha256));
    }
    [Fact]
    public void Document_hash_changes_when_text_changes_and_stale_hash_cannot_be_accepted()
    {
        using var fixture=new CatalogFixture();var before=fixture.Catalog.Read().Single(d=>d.Id=="terms");
        fixture.Write("Changed synthetic text");var after=fixture.Catalog.Read().Single(d=>d.Id=="terms");
        Assert.NotEqual(before.Sha256,after.Sha256);
        Assert.Throws<ConsentConflictException>(()=>fixture.Catalog.Validate(new("terms",before.Version,before.Sha256,"accepted","course_rights",Guid.NewGuid())));
    }

    [Theory]
    [InlineData("privacy","terms","accepted")]
    [InlineData("terms","marketing","accepted")]
    [InlineData("terms","course_rights","maybe")]
    [InlineData("terms","unknown","accepted")]
    public void Wrong_purpose_or_decision_cannot_be_accepted(string id,string purpose,string decision)
    {
        using var fixture=new CatalogFixture();var terms=fixture.Catalog.Read().First();
        Assert.Throws<ArgumentException>(()=>fixture.Catalog.Validate(new(id,terms.Version,terms.Sha256,decision,purpose,Guid.NewGuid())));
    }

    [Fact]
    public void Unknown_version_and_empty_intent_are_rejected()
    {
        using var f=new CatalogFixture();var d=f.Catalog.Read().First();
        Assert.Throws<ConsentConflictException>(()=>f.Catalog.Validate(new(d.Id,"old",d.Sha256,"accepted","terms",Guid.NewGuid())));
        Assert.Throws<ArgumentException>(()=>f.Catalog.Validate(new(d.Id,d.Version,d.Sha256,"accepted","terms",Guid.Empty)));
    }

    [Fact]
    public async Task Ledger_preserves_idempotency_account_isolation_and_revocation()
    {
        await using var f=await ApiFixture.StartAsync();
        var a=await f.AccountAsync("email","consent-a");var b=await f.AccountAsync("email","consent-b");
        using var catalog=new CatalogFixture();var d=catalog.Catalog.Read().First();
        var store=f.Service<ConsentStore>();var intent=Guid.NewGuid();
        var request=new ConsentRequest(d.Id,d.Version,d.Sha256,"accepted","course_rights",intent);
        var first=await store.RecordAsync(a.Id,request,catalog.Catalog.Snapshot(d.Id,d.Sha256),default);
        var repeat=await store.RecordAsync(a.Id,request,catalog.Catalog.Snapshot(d.Id,d.Sha256),default);Assert.Equal(first.Sequence,repeat.Sequence);
        Assert.Single(await store.ReadAsync(a.Id,default));Assert.Empty(await store.ReadAsync(b.Id,default));
        Assert.True(await store.HasCourseRightsAsync(a.Id,intent,d,default));Assert.False(await store.HasCourseRightsAsync(b.Id,intent,d,default));
        await Assert.ThrowsAsync<ConsentConflictException>(()=>store.RecordAsync(a.Id,request with{Decision="revoked"},catalog.Catalog.Snapshot(d.Id,d.Sha256),default));
        await store.RecordAsync(a.Id,request with{IntentId=Guid.NewGuid(),OperationId=intent,Decision="revoked"},catalog.Catalog.Snapshot(d.Id,d.Sha256),default);
        Assert.False(await store.HasCourseRightsAsync(a.Id,intent,d,default));Assert.Equal(2,(await store.ReadAsync(a.Id,default)).Count);
    }

    [Fact]
    public async Task Anonymous_cannot_read_or_write_ledger_and_invalid_hash_is_not_stored()
    {
        await using var f=await ApiFixture.StartAsync();
        Assert.Equal(HttpStatusCode.Unauthorized,(await f.Anonymous.GetAsync("/v1/consents")).StatusCode);
        var body=new ConsentRequest("terms","v1",new string('a',64),"accepted","terms",Guid.NewGuid());
        Assert.Equal(HttpStatusCode.Unauthorized,(await f.Anonymous.PostAsJsonAsync("/v1/consents",body)).StatusCode);
    }

    [Fact]
    public async Task Http_acceptance_binds_server_document_and_does_not_affect_another_account()
    {
        await using var f=await ApiFixture.StartAsync();using var catalog=new CatalogFixture();
        f.ProductDocumentsPath=catalog.ContentPath;await f.RestartAsync();
        var a=await f.AccountAsync("email","http-consent-a");var b=await f.AccountAsync("email","http-consent-b");
        var result=await f.Anonymous.GetFromJsonAsync<JsonElement>("/v1/documents");
        var doc=result.GetProperty("documents").EnumerateArray().First(d=>d.GetProperty("id").GetString()=="terms");
        var version=doc.GetProperty("version").GetString()!;var hash=doc.GetProperty("sha256").GetString()!;
        var request=new ConsentRequest("terms",version,hash,"accepted","course_rights",Guid.NewGuid());
        Assert.Equal(HttpStatusCode.Conflict,(await a.Client.PostAsJsonAsync("/v1/consents",request with{DocumentHash=new string('0',64)})).StatusCode);
        Assert.Equal(HttpStatusCode.OK,(await a.Client.PostAsJsonAsync("/v1/consents",request)).StatusCode);
        Assert.Equal(HttpStatusCode.OK,(await a.Client.PostAsJsonAsync("/v1/consents",request)).StatusCode);
        var own=await a.Client.GetFromJsonAsync<JsonElement>("/v1/consents");var other=await b.Client.GetFromJsonAsync<JsonElement>("/v1/consents");
        Assert.Equal(1,own.GetProperty("events").GetArrayLength());Assert.Equal(0,other.GetProperty("events").GetArrayLength());
        Assert.DoesNotContain("source",own.ToString(),StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Archived_text_is_immutable_and_changed_text_requires_new_document_version()
    {
        await using var f=await ApiFixture.StartAsync();using var catalog=new CatalogFixture();
        var a=await f.AccountAsync("email","archive-consent");var store=f.Service<ConsentStore>();
        var d=catalog.Catalog.Read().First();var oldText=catalog.Catalog.Snapshot(d.Id,d.Sha256);
        await store.RecordAsync(a.Id,new(d.Id,d.Version,d.Sha256,"accepted","terms",Guid.NewGuid()),oldText,default);
        catalog.Write("Different synthetic text");var next=catalog.Catalog.Read().First();
        await Assert.ThrowsAsync<ConsentConflictException>(()=>store.RecordAsync(a.Id,new(next.Id,next.Version,next.Sha256,"accepted","terms",Guid.NewGuid()),catalog.Catalog.Snapshot(next.Id,next.Sha256),default));
        await using var c=await f.Database.OpenConnectionAsync();
        await using var cmd=new Npgsql.NpgsqlCommand("select content_text from licensing.document_versions where document_id='terms' and document_version='v1'",c);
        Assert.Equal(oldText,await cmd.ExecuteScalarAsync());
        await using(var role=new Npgsql.NpgsqlCommand("set role vg_ledger",c))await role.ExecuteNonQueryAsync();
        await using var update=new Npgsql.NpgsqlCommand("update licensing.consent_events set decision='revoked'",c);
        var error=await Assert.ThrowsAsync<Npgsql.PostgresException>(()=>update.ExecuteNonQueryAsync());
        Assert.Equal("42501",error.SqlState);
    }

    private sealed class CatalogFixture:IDisposable
    {
        private readonly string _directory=Path.Combine(Path.GetTempPath(),"VideoGrabber-ConsentTests",Guid.NewGuid().ToString("N"));
        public DocumentCatalog Catalog{get;}
        public string ContentPath=>System.IO.Path.Combine(_directory,"content.json");
        public CatalogFixture(){Directory.CreateDirectory(_directory);Catalog=new(ContentPath);Write("Synthetic terms fixture");}
        public void Write(string text)=>File.WriteAllText(Path.Combine(_directory,"content.json"),JsonSerializer.Serialize(new{documents=new[]{new{id="terms",title="Synthetic",version="v1",sections=new[]{new{title="Test",body=text}}},new{id="consent",title="Synthetic marketing",version="v1",sections=new[]{new{title="Test",body="Synthetic"}}}}}));
        public void Dispose()=>Directory.Delete(_directory,true);
    }
}
