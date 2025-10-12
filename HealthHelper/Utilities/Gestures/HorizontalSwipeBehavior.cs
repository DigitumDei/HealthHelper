using System;
using System.Threading.Tasks;
using System.Windows.Input;
using Microsoft.Maui.Controls;

namespace HealthHelper.Utilities.Gestures;

public class HorizontalSwipeBehavior : Behavior<View>
{
    public static readonly BindableProperty SwipeLeftCommandProperty =
        BindableProperty.Create(nameof(SwipeLeftCommand), typeof(ICommand), typeof(HorizontalSwipeBehavior));

    public static readonly BindableProperty SwipeLeftCommandParameterProperty =
        BindableProperty.Create(nameof(SwipeLeftCommandParameter), typeof(object), typeof(HorizontalSwipeBehavior));

    public static readonly BindableProperty SwipeRightCommandProperty =
        BindableProperty.Create(nameof(SwipeRightCommand), typeof(ICommand), typeof(HorizontalSwipeBehavior));

    public static readonly BindableProperty SwipeRightCommandParameterProperty =
        BindableProperty.Create(nameof(SwipeRightCommandParameter), typeof(object), typeof(HorizontalSwipeBehavior));

    private PanGestureRecognizer? _panGestureRecognizer;
    private bool _isHorizontalGesture;
    private double _totalX;
    private double _totalY;

    public double SwipeThreshold { get; set; } = 120;

    public double VerticalTolerance { get; set; } = 24;

    public double FeedbackTranslationRatio { get; set; } = 0.25;

    public double MaxFeedbackTranslation { get; set; } = 48;

    public ICommand? SwipeLeftCommand
    {
        get => (ICommand?)GetValue(SwipeLeftCommandProperty);
        set => SetValue(SwipeLeftCommandProperty, value);
    }

    public object? SwipeLeftCommandParameter
    {
        get => GetValue(SwipeLeftCommandParameterProperty);
        set => SetValue(SwipeLeftCommandParameterProperty, value);
    }

    public ICommand? SwipeRightCommand
    {
        get => (ICommand?)GetValue(SwipeRightCommandProperty);
        set => SetValue(SwipeRightCommandProperty, value);
    }

    public object? SwipeRightCommandParameter
    {
        get => GetValue(SwipeRightCommandParameterProperty);
        set => SetValue(SwipeRightCommandParameterProperty, value);
    }

    protected override void OnAttachedTo(View bindable)
    {
        base.OnAttachedTo(bindable);

        _panGestureRecognizer = new PanGestureRecognizer();
        _panGestureRecognizer.PanUpdated += OnPanUpdated;
        bindable.GestureRecognizers.Add(_panGestureRecognizer);
    }

    protected override void OnDetachingFrom(View bindable)
    {
        base.OnDetachingFrom(bindable);

        if (_panGestureRecognizer is not null)
        {
            _panGestureRecognizer.PanUpdated -= OnPanUpdated;
            bindable.GestureRecognizers.Remove(_panGestureRecognizer);
            _panGestureRecognizer = null;
        }
    }

    private void OnPanUpdated(object? sender, PanUpdatedEventArgs e)
    {
        if (AssociatedObject is not View view)
        {
            return;
        }

        switch (e.StatusType)
        {
            case GestureStatus.Started:
                _isHorizontalGesture = true;
                _totalX = 0;
                _totalY = 0;
                break;
            case GestureStatus.Running:
                if (!_isHorizontalGesture)
                {
                    return;
                }

                _totalX = e.TotalX;
                _totalY = e.TotalY;

                if (Math.Abs(_totalY) > Math.Abs(_totalX) + VerticalTolerance)
                {
                    _isHorizontalGesture = false;
                    _ = ResetViewAsync(view);
                    return;
                }

                var translation = Math.Clamp(_totalX * FeedbackTranslationRatio, -MaxFeedbackTranslation, MaxFeedbackTranslation);
                view.TranslationX = translation;
                break;
            case GestureStatus.Canceled:
                _ = ResetViewAsync(view);
                break;
            case GestureStatus.Completed:
                HandleSwipeCompletion(view);
                break;
        }
    }

    private async void HandleSwipeCompletion(View view)
    {
        try
        {
            if (_isHorizontalGesture && Math.Abs(_totalX) > SwipeThreshold && Math.Abs(_totalX) > Math.Abs(_totalY))
            {
                if (_totalX > 0)
                {
                    ExecuteCommand(SwipeRightCommand, SwipeRightCommandParameter);
                }
                else
                {
                    ExecuteCommand(SwipeLeftCommand, SwipeLeftCommandParameter);
                }
            }
        }
        finally
        {
            await ResetViewAsync(view).ConfigureAwait(false);
        }
    }

    private static void ExecuteCommand(ICommand? command, object? parameter)
    {
        if (command is null)
        {
            return;
        }

        if (command.CanExecute(parameter))
        {
            command.Execute(parameter);
        }
    }

    private static async Task ResetViewAsync(View view)
    {
        if (Math.Abs(view.TranslationX) < 1)
        {
            view.TranslationX = 0;
            return;
        }

        await view.TranslateTo(0, 0, 120, Easing.SinOut).ConfigureAwait(false);
        view.TranslationX = 0;
    }
}
