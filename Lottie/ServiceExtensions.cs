using Avalonia.Markup.Xaml;

namespace Lottie;

internal static class ServiceExtensions
{
    private static T Resolve<T>(this IServiceProvider sp) => (T)sp.GetService(typeof(T))!;
    public static Uri GetContextBaseUri(this IServiceProvider sp)
        => sp.Resolve<IUriContext>().BaseUri;
}