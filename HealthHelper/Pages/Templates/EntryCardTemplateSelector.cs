using HealthHelper.Models;
using Microsoft.Maui.Controls;

namespace HealthHelper.Pages.Templates;

public class EntryCardTemplateSelector : DataTemplateSelector
{
    public DataTemplate? MealTemplate { get; set; }
    public DataTemplate? ExerciseTemplate { get; set; }
    public DataTemplate? SleepTemplate { get; set; }

    protected override DataTemplate OnSelectTemplate(object item, BindableObject container)
    {
        switch (item)
        {
            case MealPhoto:
                if (MealTemplate is null)
                {
                    throw new InvalidOperationException("MealTemplate must be provided for EntryCardTemplateSelector.");
                }
                return MealTemplate;
            case ExerciseEntry:
                if (ExerciseTemplate is null)
                {
                    throw new InvalidOperationException("ExerciseTemplate must be provided for EntryCardTemplateSelector.");
                }
                return ExerciseTemplate;
            case SleepEntry:
                if (SleepTemplate is null)
                {
                    throw new InvalidOperationException("SleepTemplate must be provided for EntryCardTemplateSelector.");
                }
                return SleepTemplate;
            default:
                if (MealTemplate is not null)
                {
                    return MealTemplate;
                }

                throw new InvalidOperationException($"No template configured for item type {item?.GetType().Name ?? "null"}.");
        }
    }
}
