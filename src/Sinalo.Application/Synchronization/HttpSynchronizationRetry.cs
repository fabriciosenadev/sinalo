using System.Net;

namespace Sinalo.Application.Synchronization;

public static class HttpSynchronizationRetry
{
    private const int MaximumAttempts = 3;

    public static async Task<string> GetStringAsync(HttpClient client, Uri uri, CancellationToken cancellationToken = default)
    {
        using var response = await GetAsync(client, uri, cancellationToken);
        return await response.Content.ReadAsStringAsync(cancellationToken);
    }

    public static async Task<HttpResponseMessage> GetAsync(HttpClient client, Uri uri, CancellationToken cancellationToken = default)
    {
        Exception? lastException = null;
        for (var attempt = 1; attempt <= MaximumAttempts; attempt++)
        {
            try
            {
                var response = await client.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
                if (!IsTransient(response.StatusCode) || attempt == MaximumAttempts)
                {
                    response.EnsureSuccessStatusCode();
                    return response;
                }

                response.Dispose();
            }
            catch (HttpRequestException exception) when (exception.StatusCode is null && attempt < MaximumAttempts)
            {
                lastException = exception;
            }
            catch (TaskCanceledException exception) when (!cancellationToken.IsCancellationRequested && attempt < MaximumAttempts)
            {
                lastException = exception;
            }

            await Task.Delay(TimeSpan.FromSeconds(attempt), cancellationToken);
        }

        throw lastException ?? new HttpRequestException("Não foi possível obter resposta do site oficial.");
    }

    private static bool IsTransient(HttpStatusCode statusCode) => (int)statusCode >= 500;
}
