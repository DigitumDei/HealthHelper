using CommunityToolkit.Mvvm.Input;
using HealthHelper.Models;

namespace HealthHelper.PageModels;

public interface IProjectTaskPageModel
{
	IAsyncRelayCommand<ProjectTask> NavigateToTaskCommand { get; }
	bool IsBusy { get; }
}