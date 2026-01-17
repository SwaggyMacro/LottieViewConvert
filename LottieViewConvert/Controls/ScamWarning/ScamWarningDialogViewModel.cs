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
    private const string ParagraphSeparator = "\n\n";

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

        var content = Resources.ScamWarningContent;
        RepoUrl = ExtractRepoUrl(content) ?? FallbackRepoUrl;
        var normalizedContent = content.Replace("\r\n", "\n");
        var paragraphs = normalizedContent.Split(ParagraphSeparator, StringSplitOptions.RemoveEmptyEntries);
        var header = paragraphs.FirstOrDefault() ?? content;

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

        ScamWarningLine = paragraphs.Length > 1 ? paragraphs[1] : string.Empty;
        StarLine = paragraphs.Length > 2 ? paragraphs[2] : string.Empty;

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
}
