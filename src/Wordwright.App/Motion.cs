using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace Wordwright.App;

/// <summary>
/// The one motion system for the shipping app (docs/DESIGN.md → "Motion";
/// decision D7). One entrance — fade plus an 8 px rise, 200 ms decelerate —
/// one exit (fade, 120 ms accelerate), the connected-animation glide, and a
/// staggered settle built from the entrance. Every method is a strict no-op
/// when Windows animations are off, every animation stops rather than holding a
/// property, and nothing repeats or loops.
/// </summary>
public static class Motion
{
    private static readonly Duration EnterDuration = TimeSpan.FromMilliseconds(200);
    private static readonly Duration ExitDuration = TimeSpan.FromMilliseconds(120);
    private const double RiseDistance = 8;
    private const int SettleStepMs = 30;

    /// <summary>False when "Show animations in Windows" is off, so callers and
    /// the methods below behave exactly as if motion did not exist.</summary>
    public static bool Enabled => SystemParameters.ClientAreaAnimation;

    /// <summary>Fade and rise an element into place. Safe to call on an element
    /// that is already visible; the final state is the element's normal one.</summary>
    public static void Enter(FrameworkElement element, int delayMs = 0)
    {
        if (!Enabled)
        {
            return;
        }

        // Base values are the finished state, so FillBehavior.Stop leaves the
        // element correct even if the animation is replaced before it ends.
        element.Opacity = 1;
        var translate = EnsureTranslate(element);
        translate.Y = 0;

        var easing = new CubicEase { EasingMode = EasingMode.EaseOut };
        var delay = TimeSpan.FromMilliseconds(delayMs);

        element.BeginAnimation(
            UIElement.OpacityProperty,
            new DoubleAnimation(0, 1, EnterDuration) { BeginTime = delay, EasingFunction = easing, FillBehavior = FillBehavior.Stop });
        translate.BeginAnimation(
            TranslateTransform.YProperty,
            new DoubleAnimation(RiseDistance, 0, EnterDuration) { BeginTime = delay, EasingFunction = easing, FillBehavior = FillBehavior.Stop });
    }

    /// <summary>Fade an element out, then report completion. The callback always
    /// runs — even with animations off — so callers can defer removal to it.</summary>
    public static void Exit(UIElement element, Action? completed = null)
    {
        if (!Enabled)
        {
            completed?.Invoke();
            return;
        }

        var fade = new DoubleAnimation(element.Opacity, 0, ExitDuration)
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn },
            FillBehavior = FillBehavior.Stop,
        };
        fade.Completed += (_, _) =>
        {
            element.Opacity = 0;
            completed?.Invoke();
        };
        element.BeginAnimation(UIElement.OpacityProperty, fade);
    }

    /// <summary>The D7 signature: glide a floating proxy from one on-screen rect
    /// to another (position and size), then report completion. The proxy is
    /// expected to sit on a Canvas; its base geometry is left at the target.</summary>
    public static void Glide(FrameworkElement proxy, Rect from, Rect to, Action? completed = null)
    {
        if (!Enabled)
        {
            completed?.Invoke();
            return;
        }

        Canvas.SetLeft(proxy, to.X);
        Canvas.SetTop(proxy, to.Y);
        proxy.Width = to.Width;
        proxy.Height = to.Height;

        var easing = new CubicEase { EasingMode = EasingMode.EaseOut };
        proxy.BeginAnimation(
            Canvas.LeftProperty,
            new DoubleAnimation(from.X, to.X, EnterDuration) { EasingFunction = easing, FillBehavior = FillBehavior.Stop });
        proxy.BeginAnimation(
            Canvas.TopProperty,
            new DoubleAnimation(from.Y, to.Y, EnterDuration) { EasingFunction = easing, FillBehavior = FillBehavior.Stop });
        proxy.BeginAnimation(
            FrameworkElement.WidthProperty,
            new DoubleAnimation(from.Width, to.Width, EnterDuration) { EasingFunction = easing, FillBehavior = FillBehavior.Stop });

        var height = new DoubleAnimation(from.Height, to.Height, EnterDuration)
        {
            EasingFunction = easing,
            FillBehavior = FillBehavior.Stop,
        };
        height.Completed += (_, _) => completed?.Invoke();
        proxy.BeginAnimation(FrameworkElement.HeightProperty, height);
    }

    /// <summary>Enter a small set of elements in sequence (editor settle).</summary>
    public static void Settle(params FrameworkElement[] elements)
    {
        for (var index = 0; index < elements.Length; index++)
        {
            Enter(elements[index], index * SettleStepMs);
        }
    }

    /// <summary>Gives the element a <see cref="TranslateTransform"/> to animate,
    /// reusing one it already has. Callers pass plain layout elements that carry
    /// no render transform of their own.</summary>
    private static TranslateTransform EnsureTranslate(FrameworkElement element)
    {
        if (element.RenderTransform is TranslateTransform existing)
        {
            return existing;
        }

        var translate = new TranslateTransform();
        element.RenderTransform = translate;
        return translate;
    }
}
