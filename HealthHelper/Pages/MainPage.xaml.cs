using HealthHelper.PageModels;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Media;
using Microsoft.Maui.Storage;

namespace HealthHelper.Pages;

public partial class MainPage : ContentPage
{
	public MainPage(MealLogViewModel viewModel)
	{
		InitializeComponent();
		BindingContext = viewModel;
	}

	private async void TakePhotoButton_Clicked(object sender, EventArgs e)
	{
		try
		{
			if (!await EnsureCameraPermissionsAsync())
			{
				return;
			}

			if (!MediaPicker.Default.IsCaptureSupported)
			{
				string? placeholderPath = await CreatePlaceholderMealPhotoAsync();
				if (placeholderPath is not null && BindingContext is MealLogViewModel placeholderViewModel)
				{
					await DisplayAlertAsync("Emulator", "Camera capture isn't available here, so we saved a sample meal photo instead.", "OK");
					placeholderViewModel.AddMealPhoto(placeholderPath);
				}
				return;
			}

			FileResult? photo = await MediaPicker.Default.CapturePhotoAsync();

			if (photo is null)
			{
				return;
			}

			string fileName = photo.FileName;
			if (string.IsNullOrWhiteSpace(fileName))
			{
				fileName = $"photo_{DateTime.UtcNow:yyyyMMdd_HHmmssfff}.jpg";
			}

			string localFilePath = Path.Combine(FileSystem.CacheDirectory, fileName);

			await using Stream sourceStream = await photo.OpenReadAsync();
			await using FileStream localFileStream = File.Open(localFilePath, FileMode.Create, FileAccess.Write);
			await sourceStream.CopyToAsync(localFileStream);

			(BindingContext as MealLogViewModel)?.AddMealPhoto(localFilePath);
		}
		catch (FeatureNotSupportedException ex)
		{
			string? placeholderPath = await CreatePlaceholderMealPhotoAsync();
			if (placeholderPath is not null && BindingContext is MealLogViewModel placeholderViewModel)
			{
				await DisplayAlertAsync("Emulator", "Camera capture isn't available here, so we saved a sample meal photo instead.", "OK");
				placeholderViewModel.AddMealPhoto(placeholderPath);
			}
		}
		catch (PermissionException)
		{
			await DisplayAlertAsync("Permissions", "Camera permission is required to take photos.", "OK");
		}
		catch (Exception)
		{
			await DisplayAlertAsync("Error", "We couldn't capture a photo. Please try again.", "OK");
		}
	}

	private async Task<bool> EnsureCameraPermissionsAsync()
	{
		PermissionStatus cameraStatus = await Permissions.CheckStatusAsync<Permissions.Camera>();
		if (cameraStatus != PermissionStatus.Granted)
		{
			if (Permissions.ShouldShowRationale<Permissions.Camera>())
			{
				await DisplayAlertAsync("Permissions", "Camera access is required to take a photo.", "OK");
			}

			cameraStatus = await Permissions.RequestAsync<Permissions.Camera>();
		}

		if (cameraStatus != PermissionStatus.Granted)
		{
			return false;
		}

#if ANDROID
		PermissionStatus storageStatus;
		if (OperatingSystem.IsAndroidVersionAtLeast(33))
		{
			storageStatus = await Permissions.CheckStatusAsync<Permissions.Media>();
			if (storageStatus != PermissionStatus.Granted)
			{
				storageStatus = await Permissions.RequestAsync<Permissions.Media>();
			}
		}
		else
		{
			storageStatus = await Permissions.CheckStatusAsync<Permissions.StorageWrite>();
			if (storageStatus != PermissionStatus.Granted)
			{
				storageStatus = await Permissions.RequestAsync<Permissions.StorageWrite>();
			}
		}

		if (storageStatus != PermissionStatus.Granted)
		{
			return false;
		}
#endif
		return true;
	}

	private static async Task<string?> CreatePlaceholderMealPhotoAsync()
	{
		try
		{
			await using Stream stream = await FileSystem.OpenAppPackageFileAsync("sample-meal.png");
			string fileName = $"sample_{DateTime.UtcNow:yyyyMMdd_HHmmssfff}.png";
			string destination = Path.Combine(FileSystem.CacheDirectory, fileName);
			await using FileStream fileStream = File.Open(destination, FileMode.Create, FileAccess.Write);
			await stream.CopyToAsync(fileStream);
			return destination;
		}
		catch
		{
			return null;
		}
	}
}
