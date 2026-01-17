using System;
using System.Linq;
using System.Reactive;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using LottieViewConvert.Helper.LogHelper;
using LottieViewConvert.Lang;
using LottieViewConvert.Services;
using ReactiveUI;
using SukiUI.Dialogs;

namespace LottieViewConvert.Controls.ScamWarning;

public class ScamWarningDialogViewModel : ReactiveObject
{
    private const string FallbackRepoUrl = "https://github.com/SwaggyMacro/LottieViewConvert";
    private const int HeaderIndex = 0;
    private const int ScamWarningIndex = 1;
    private const int StarIndex = 2;

    private readonly ISukiDialog _dialog;
    private readonly ConfigService _configService;

    public string HeaderPrefix { get; }
    public string HeaderSuffix { get; }
    public string RepoUrl { get; }
    public string ScamWarningLine { get; }
    public string StarLine { get; }

    public bool HasHeaderSuffix => !string.IsNullOrWhiteSpace(HeaderSuffix);
    public bool HasScamWarningLine => !string.IsNullOrWhiteSpace(ScamWarningLine);
    public bool HasStarLine => !string.IsNullOrWhiteSpace(StarLine);

    public ReactiveCommand<Unit, Unit> GotItCommand { get; }
    public ReactiveCommand<Unit, Unit> DontShowAgainCommand { get; }
    public ReactiveCommand<string, Unit> OpenLinkCommand { get; }

    public ScamWarningDialogViewModel(
        ISukiDialog dialog,
        ConfigService configService,
        ReactiveCommand<string, Unit> openLinkCommand)
    {
        _dialog = dialog;
        _configService = configService;
        OpenLinkCommand = openLinkCommand;

        var content = NormalizeContent(Resources.ScamWarningContent);
        RepoUrl = ExtractRepoUrl(content) ?? FallbackRepoUrl;
        var paragraphs = Regex.Split(content, @"\n{2,}")
            .Where(paragraph => !string.IsNullOrWhiteSpace(paragraph))
            .ToArray();
        var header = paragraphs.Length > HeaderIndex ? paragraphs[HeaderIndex] : content;

        var urlIndex = header.IndexOf(RepoUrl, StringComparison.Ordinal);
        if (urlIndex >= 0)
        {
            HeaderPrefix = header[..urlIndex].TrimEnd();
            HeaderSuffix = header[(urlIndex + RepoUrl.Length)..].TrimStart();
        }
        else
        {
            HeaderPrefix = header;
            HeaderSuffix = string.Empty;
        }

        ScamWarningLine = paragraphs.Length > ScamWarningIndex ? paragraphs[ScamWarningIndex] : string.Empty;
        StarLine = paragraphs.Length > StarIndex ? paragraphs[StarIndex] : string.Empty;

        GotItCommand = ReactiveCommand.Create(Dismiss);
        DontShowAgainCommand = ReactiveCommand.CreateFromTask(DisableScamWarningAsync);
    }

    private async Task DisableScamWarningAsync()
    {
        try
        {
            var config = await _configService.LoadConfigAsync();
            config.ShowScamWarningDialog = false;
            await _configService.SaveConfigAsync(config);
        }
        catch (Exception ex)
        {
            Logger.Error($"Failed to update scam warning preference: {ex.Message}");
        }
        finally
        {
            _dialog.Dismiss();
        }
    }

    private void Dismiss()
    {
        _dialog.Dismiss();
    }

    private static string? ExtractRepoUrl(string content)
    {
        var match = Regex.Match(content, @"https?://\S+");
        return match.Success ? match.Value : null;
    }

    private static string NormalizeContent(string content)
    {
        return Regex.Replace(content, @"\r\n?", "\n");
    }
}
