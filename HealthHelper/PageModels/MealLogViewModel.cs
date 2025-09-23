using System.Collections.ObjectModel;
using HealthHelper.Models;

namespace HealthHelper.PageModels;

public class MealLogViewModel
{
    public ObservableCollection<MealPhoto> Meals { get; } = new();

    public void AddMealPhoto(string filePath)
    {
        Meals.Insert(0, new MealPhoto(filePath, DateTimeOffset.UtcNow));
    }
}
