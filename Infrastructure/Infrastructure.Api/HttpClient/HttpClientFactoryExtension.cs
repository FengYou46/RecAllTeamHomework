namespace RecAll.Infrastructure.Api.HttpClient;

public static class HttpClientFactoryExtension {
    public const string DefaultClient = nameof(DefaultClient);
    public const string RawClient = nameof(RawClient);

    public static System.Net.Http.HttpClient CreateDefaultClient(
        this IHttpClientFactory httpClientFactory) =>
        httpClientFactory.CreateClient(DefaultClient);

    public static System.Net.Http.HttpClient CreateRawClient(
        this IHttpClientFactory httpClientFactory) =>
        httpClientFactory.CreateClient(RawClient);
}