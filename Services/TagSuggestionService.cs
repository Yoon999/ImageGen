using System.Collections.ObjectModel;
using System.Windows;
using ImageGen.Helpers;
using ImageGen.Models.Api;
using ImageGen.Services.Interfaces;
using Application = System.Windows.Application;

namespace ImageGen.Services;

public class TagSuggestionService
{
    private readonly INovelAiService _novelAiService;
    private CancellationTokenSource? _debounceCts;

    public TagSuggestionService(INovelAiService novelAiService)
    {
        _novelAiService = novelAiService;
    }

    public void Search(
        string query,
        string model,
        string accessToken,
        ObservableCollection<TagSuggestion> target)
    {
        _debounceCts?.Cancel();
        _debounceCts = new CancellationTokenSource();
        var token = _debounceCts.Token;

        _ = SearchAsync(query, model, accessToken, target, token);
    }

    private async Task SearchAsync(
        string query,
        string model,
        string accessToken,
        ObservableCollection<TagSuggestion> target,
        CancellationToken token)
    {
        try
        {
            await Task.Delay(300, token);

            if (string.IsNullOrWhiteSpace(query) || query.Length < 2)
            {
                Dispatch(() => target.Clear());
                return;
            }

            if (string.IsNullOrWhiteSpace(accessToken))
            {
                return;
            }

            var suggestions = await _novelAiService.SuggestTagsAsync(query, model, accessToken);
            if (token.IsCancellationRequested)
            {
                return;
            }

            Dispatch(() =>
            {
                target.Clear();
                foreach (var tag in suggestions)
                {
                    target.Add(tag);
                }
            });
        }
        catch (OperationCanceledException)
        {
            // A newer search request replaced this one.
        }
        catch (Exception ex)
        {
            Logger.LogError("Tag suggestion failed", ex);
        }
    }

    private static void Dispatch(Action action)
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher == null || dispatcher.CheckAccess())
        {
            action();
            return;
        }

        dispatcher.Invoke(action);
    }
}
